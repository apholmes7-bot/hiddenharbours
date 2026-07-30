#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tools.RigStudio
{
    /// <summary>
    /// The ROCK ISO KIT's Rig Studio adapter — the thinnest of the set, which is the point of doing
    /// it last: species × stone × tide × dress × variant, every value from the kit's committed
    /// <c>RockIso.json</c> (the art director's oracle — this kit's bake NEVER rewrites it, only
    /// refuses against it).
    ///
    /// <para><b>The one wrinkle the axis model meets here:</b> the variant count is per SPECIES (an
    /// Erratic has four, a Cobble three — the contract's own columns), and a schema axis has one
    /// bound. So the axis carries the widest count and the preview refuses a variant the viewed
    /// species does not have, naming the species' own count — the same cross-axis pattern as the
    /// shrub kit's buried-species refusal, and further evidence the seam holds: cross-axis legality
    /// is adapter business, not studio business.</para>
    ///
    /// <para><b>Preview</b> = the baker's own <see cref="RockIsoBaker.ResultExpr"/> (albedo — this is
    /// a single-channel kit), after <see cref="RockIsoBaker.AssertMatchesContract"/>, with the
    /// VARIANT's own ground-contact pivot from the contract. <b>Bake</b> = the kit's menu chain
    /// scoped to the viewed species × stone × tide: one sheet (all variants × dress rows — the grid
    /// the owner's cell lives in), slice, verify. This kit ships no pixels (#312): output lands
    /// uncommitted, to order.</para>
    /// </summary>
    public sealed class RockIsoStudioAdapter : IRigStudioAdapter
    {
        public const string SpeciesAxis = "species";
        public const string StoneAxis = "stone";
        public const string TideAxis = "tide";
        public const string DressAxis = "dress";
        public const string VariantAxis = "variant";

        IRigScriptHost _host;
        RockIsoCatalog.Contract _contract;
        RigStudioSchema _schema;

        RockIsoBaker.SpeciesSpec _spec;
        string _specKey;

        RockIsoCatalog.Contract Contract()
        {
            if (_contract != null) return _contract;
            _contract = RockIsoCatalog.Load()
                ?? throw new InvalidOperationException(
                    $"{RockIsoCatalog.ContractPath} is missing or unreadable — it ships WITH the " +
                    "kit (the art director's oracle), so a missing contract means the branch " +
                    "predates the Rock Iso import.");
            return _contract;
        }

        public RigStudioSchema Describe()
        {
            if (_schema != null) return _schema;
            RockIsoCatalog.Contract contract = Contract();

            string[] species = contract.Species.Select(s => s.Key).ToArray();
            int widestVariants = contract.Species.Max(s => s.Variants.Count);

            _schema = new RigStudioSchema
            {
                KitName = "Shore rocks",
                About = "Six rock forms × four stones × three tides — the shore's standing " +
                        "hazards, perches and pools.",
                BakeNote = "Bake this bakes ONE sheet — the viewed species at the viewed stone and " +
                           "tide, all its variants × dress rows — then slices and verifies against " +
                           "RockIso.json (which is never rewritten: it is the art director's " +
                           "oracle). This kit ships no pixels: the sheet lands uncommitted, to " +
                           "order.",
                Axes = new[]
                {
                    new RigStudioAxis
                    {
                        Key = SpeciesAxis, Label = "Form", Kind = RigStudioAxisKind.Keys,
                        Keys = species, Default = species[0],
                    },
                    new RigStudioAxis
                    {
                        Key = StoneAxis, Label = "Stone", Kind = RigStudioAxisKind.Keys,
                        Keys = contract.Stones.ToArray(), Default = RockIsoBaker.DefaultStone,
                    },
                    new RigStudioAxis
                    {
                        Key = TideAxis, Label = "Tide", Kind = RigStudioAxisKind.Keys,
                        Keys = contract.Tides.ToArray(), Default = contract.Tides[0],
                    },
                    new RigStudioAxis
                    {
                        Key = DressAxis, Label = "Dress", Kind = RigStudioAxisKind.Keys,
                        Keys = contract.Dress.ToArray(), Default = contract.Dress[0],
                        Note = "the sheet row — bare, barnacled or weeded",
                    },
                    new RigStudioAxis
                    {
                        Key = VariantAxis, Label = "Variant", Kind = RigStudioAxisKind.Seed,
                        Min = 0, Max = widestVariants - 1, Default = 0,
                        Note = "the variant count is per form (the contract's own columns) — a " +
                               "variant the viewed form does not have refuses with its real count",
                    },
                },
            };
            return _schema;
        }

        public RigStudioPreview Preview(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);
            RockIsoCatalog.Contract contract = Contract();

            string species = RigStudioPoints.KeyOf(point, SpeciesAxis);
            string stone = RigStudioPoints.KeyOf(point, StoneAxis);
            string tide = RigStudioPoints.KeyOf(point, TideAxis);
            string dress = RigStudioPoints.KeyOf(point, DressAxis);
            int variant = RigStudioPoints.IntOf(point, VariantAxis);

            RockIsoCatalog.SpeciesEntry entry = contract.Find(species)
                ?? throw new ArgumentException(
                    $"'{species}' passed the axis check but RockIso.json does not carry it — the " +
                    "adapter's schema is lying about the kit. Refusing to render anything in its " +
                    "place.");

            // Cross-axis legality, from the contract's own columns: the widest species sets the
            // axis bound, the viewed species sets the law.
            if (variant >= entry.Variants.Count)
                throw new ArgumentException(
                    $"'{species}' has {entry.Variants.Count} variant(s) in RockIso.json — variant " +
                    $"{variant} does not exist. Rendering some other rock under this number is " +
                    "exactly the silent substitution this studio refuses.");

            int dressRow = Array.IndexOf(contract.Dress, dress);

            EnsureSpec(species);

            string expr = RockIsoBaker.ResultExpr(species, variant, dressRow, stone, tide);
            byte[] rgba = _host.EvaluateBytes($"{expr}.rgba");

            int expected = _spec.CellW * _spec.CellH * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"render came back {rgba.Length} bytes, expected {expected} for " +
                    $"{_spec.CellW}×{_spec.CellH} RGBA — the cell does not match the contract's. " +
                    "Do not show it.");

            RockIsoCatalog.VariantEntry v = entry.Variants[variant];
            return new RigStudioPreview
            {
                Rgba = rgba,
                Width = _spec.CellW,
                Height = _spec.CellH,
                HasPivot = true,
                // ⚠ Per-VARIANT ground contact — the rock kit's pivots are variant data, not sheet
                // data, which is why the slicer stamps them per rect.
                PivotX = v.Pivot.x,
                PivotY = v.Pivot.y,
                Caption = $"{entry.Name} ({species}) v{variant} — {v.TopM:0.#} m high, " +
                          $"{stone} · {tide} · {dress}; cell {_spec.CellW}×{_spec.CellH}" +
                          (v.IsAwash && v.HazardRM.HasValue
                              ? $"; AWASH hazard r {v.HazardRM.Value:0.#} m"
                              : ""),
            };
        }

        void EnsureSpec(string species)
        {
            if (_host != null && string.Equals(_specKey, species, StringComparison.Ordinal)) return;

            if (_host == null)
            {
                _host = RigScriptHostFactory.Create();
                RockIsoBaker.InstallRig(_host);
            }

            RockIsoBaker.SpeciesSpec spec = RockIsoBaker.ReadSpeciesSpec(_host, species);
            RockIsoBaker.AssertMatchesContract(species, spec, Contract());

            _spec = spec;
            _specKey = species;
        }

        public RigStudioBakeReport Bake(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);

            string species = RigStudioPoints.KeyOf(point, SpeciesAxis);
            string stone = RigStudioPoints.KeyOf(point, StoneAxis);
            string tide = RigStudioPoints.KeyOf(point, TideAxis);

            // 1) The kit's baker, scoped to the viewed sheet — its own oracle check runs inside.
            RockBakeResult result = RockIsoBaker.Bake(
                stones: new[] { stone }, tides: new[] { tide }, species: new[] { species },
                progress: (label, t) => EditorUtility.DisplayProgressBar(
                    "Rig Studio — baking rock sheet", label, t));

            // 2) The menu's import discipline, then the kit's slicer + verify.
            AssetDatabase.Refresh();
            foreach (RockSheetBake sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);

            int sliced = RockSheetSlicer.SliceAll(out int failed);
            if (failed > 0)
                throw new InvalidOperationException(
                    $"The rock slicer REFUSED {failed} sheet(s) (sliced {sliced}). Its own reasons " +
                    "are above, verbatim — nothing was guessed at.");

            if (!RockSheetSlicer.VerifyAll(logEachPass: false, out int checkedCount))
                throw new InvalidOperationException(
                    $"Rock sheet verification FAILED ({checkedCount} checked) — see the mismatches " +
                    "above. Do not commit until they are clear.");

            string stem = RockIsoCatalog.StemFor(species, stone, tide);
            RockIsoCatalog.SpeciesEntry entry = Contract().Find(species);

            return new RigStudioBakeReport
            {
                Minted = result.Sheets.Select(s => s.AssetPath).ToArray(),
                PlaceableNote =
                    $"'{species}' at {stone}/{tide} is sliced and placeable: sprites {stem}_v0_d0 … " +
                    $"{stem}_v{entry.Variants.Count - 1}_d{Contract().Dress.Length - 1} under " +
                    $"{RockIsoCatalog.RockRoot}, each variant on its own ground-contact pivot. NOT " +
                    "committed — this kit ships no pixels; committing a sheet is a deliberate call.",
                Summary = $"{result.Sheets.Count} sheet(s), {result.CellsRendered} cell(s) in " +
                          $"{result.RenderMilliseconds:F0} ms; RockIso.json was checked, never " +
                          "rewritten — it is the art director's oracle.",
            };
        }

        public void Dispose()
        {
            _host?.Dispose();
            _host = null;
            _specKey = null;
        }
    }
}
#endif
