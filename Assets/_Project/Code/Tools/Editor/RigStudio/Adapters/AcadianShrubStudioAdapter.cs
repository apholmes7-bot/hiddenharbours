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
    /// The ACADIAN SHRUB KIT's Rig Studio adapter — the abstraction's stress test, because this is the
    /// richest axis space any kit has shipped: 20 species × 3 stages × 8 calendar phases × 4 variants
    /// × 4 sway frames, with a 5-step snow law on top. Every value below is enumerated from the kit's
    /// committed contract (<c>shrubIsoRig.contract.json</c>, the bake's own oracle) — never restated.
    ///
    /// <para><b>Preview = the bake's own cell.</b> One cell through the kit's one render expression,
    /// <see cref="ShrubBaker.ResultExpr"/> — the same string the baker evaluates — after the same
    /// guard sequence a bake runs (<see cref="ShrubBaker.AssertFits"/>,
    /// <see cref="ShrubBaker.AssertMatchesContract"/>). So the preview refuses exactly where a bake
    /// refuses: a stage the committed contract was not exported at surfaces the kit's own
    /// "re-export the contract" message here, verbatim, instead of pixels no bake can ship.</para>
    ///
    /// <para><b>⭐ SNOW IS A GUIDE, NEVER PIXELS.</b> The kit's law is "snow CLIPS, it never floods":
    /// sheets bake snow-free (<see cref="ShrubBaker.ResultExpr"/> deliberately passes no snow — it is
    /// outside the union cell by construction) and the SCENE removes rows below the surface at
    /// placement, drawing its own drift. The rig's <c>render()</c> does accept a snow option, but
    /// previewing it would show crust art that no bake produces — exactly the approve-what-never-ships
    /// defect this studio exists to retire. So the snow axis draws the contract's snow SURFACE as an
    /// annotation line over the bake-identical pixels (depth = step × the species' own habitat
    /// <c>snowK</c>), and a species buried past the contract's cutoff REFUSES with the kit's own
    /// ruling: do not draw the shrub, draw a drift.</para>
    ///
    /// <para><b>Bake = the kit's menu chain, scoped to what the owner is looking at</b>: the phase-axis
    /// sheet (the gameplay sheet — "the calendar advances and every shrub follows it") of THIS species
    /// at the viewed stage and variant, three channels, then the kit's slicer + verify — the same
    /// steps <c>ShrubBakeMenu.BakeShrubSheets</c> runs, through the same public baker API it uses.
    /// Output lands uncommitted in the kit folder (this kit bakes TO ORDER and ships one named slice;
    /// the St Peters atlas guard fails on any extra committed PNG — widening it is the owner's
    /// atlas-budget decision).</para>
    /// </summary>
    public sealed class AcadianShrubStudioAdapter : IRigStudioAdapter
    {
        public const string SpeciesAxis = "species";
        public const string StageAxis = "stage";
        public const string PhaseAxis = "phase";
        public const string VariantAxis = "variant";
        public const string FrameAxis = "frame";
        public const string SnowAxis = "snow";

        IRigScriptHost _host;
        ShrubCatalog.Contract _contract;
        RigStudioSchema _schema;

        // The last (species, stage)'s sheet spec — the guard result AND the cell geometry. Cached so
        // scrubbing phase/variant/frame/snow re-runs one render, not the whole guard chain.
        ShrubBaker.SheetSpec _spec;
        string _specKey;

        ShrubCatalog.Contract Contract()
        {
            if (_contract != null) return _contract;
            _contract = ShrubCatalog.Load()
                ?? throw new InvalidOperationException(
                    $"{ShrubCatalog.ContractPath} is missing or unreadable — it is the oracle every " +
                    "shrub bake is checked against, so there is no parameter space to describe. Run " +
                    "Hidden Harbours ▸ Art ▸ Export Shrub Contract first.");
            return _contract;
        }

        public RigStudioSchema Describe()
        {
            if (_schema != null) return _schema;
            ShrubCatalog.Contract contract = Contract();

            string[] species = contract.Species.Select(s => s.Key).ToArray();
            string[] phases = contract.Calendar.Phases.Select(p => p.Key).ToArray();
            string[] snows = contract.Snow.Steps.Select(s => s.Key).ToArray();

            // The variant count is contract-carried as geometry: the variant-axis sheet is
            // (variants × cellW) wide, so cols = sheetW / cellW — read, not restated.
            var first = contract.Species[0];
            int variants = first.CellW > 0 ? first.VariantSheetW / first.CellW : ShrubCatalog.Variants;

            _schema = new RigStudioSchema
            {
                KitName = "Acadian shrubs",
                About = "Twenty woody species across five habitats, on an eight-station calendar — " +
                        "the understorey between the grass and the trees.",
                BakeNote = "Bake this bakes THIS species' phase-axis sheet (8 phases × 4 sway rows × " +
                           "3 channels) at the viewed stage and variant, then slices and verifies — " +
                           "the kit's own chain, scoped to one species. This kit bakes TO ORDER: the " +
                           "sheets land uncommitted, and committing any is an atlas-budget decision " +
                           "gated by the St Peters slice test. Snow is never baked — it clips at " +
                           "placement.",
                Axes = new[]
                {
                    new RigStudioAxis
                    {
                        Key = SpeciesAxis, Label = "Species", Kind = RigStudioAxisKind.Keys,
                        Keys = species, Default = species[0],
                    },
                    new RigStudioAxis
                    {
                        Key = StageAxis, Label = "Growth stage", Kind = RigStudioAxisKind.Keys,
                        Keys = ShrubCatalog.StageKeys.ToArray(),
                        Default = ShrubBaker.DefaultStage,
                        Note = "the committed contract is exported at one stage; the kit refuses " +
                               "the others until it is re-exported (a deliberate act) — the refusal " +
                               "shows here verbatim",
                    },
                    new RigStudioAxis
                    {
                        Key = PhaseAxis, Label = "Calendar phase", Kind = RigStudioAxisKind.Keys,
                        Keys = phases, Default = ShrubBaker.DefaultPhase,
                    },
                    new RigStudioAxis
                    {
                        Key = VariantAxis, Label = "Variant", Kind = RigStudioAxisKind.Seed,
                        Min = 0, Max = variants - 1, Default = ShrubBaker.DefaultVariant,
                    },
                    new RigStudioAxis
                    {
                        Key = FrameAxis, Label = "Sway frame", Kind = RigStudioAxisKind.IntRange,
                        Min = 0, Max = contract.Sway.Frames - 1, Default = 0,
                        Note = "the sheet row — sway is baked on this kit (pinned at the pivot and " +
                               "the snow line), unlike the trees' shader wind",
                    },
                    new RigStudioAxis
                    {
                        Key = SnowAxis, Label = "Snow", Kind = RigStudioAxisKind.Keys,
                        Keys = snows, Default = snows[0],
                        Note = "an annotation, not pixels: sheets bake snow-free and the scene " +
                               "clips rows below the line at placement",
                    },
                },
            };
            return _schema;
        }

        public RigStudioPreview Preview(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);
            ShrubCatalog.Contract contract = Contract();

            string species = RigStudioPoints.KeyOf(point, SpeciesAxis);
            string stage = RigStudioPoints.KeyOf(point, StageAxis);
            string phase = RigStudioPoints.KeyOf(point, PhaseAxis);
            int variant = RigStudioPoints.IntOf(point, VariantAxis);
            int frame = RigStudioPoints.IntOf(point, FrameAxis);
            string snow = RigStudioPoints.KeyOf(point, SnowAxis);

            ShrubCatalog.SpeciesEntry entry = contract.Find(species)
                ?? throw new ArgumentException(
                    $"'{species}' passed the axis check but the contract does not carry it — the " +
                    "adapter's schema is lying about the kit. Refusing to render anything in its place.");

            // The kit's own placement law, before any render: at or past the buried cutoff the shrub
            // is not drawn at all — the scene owes a drift there. Previewing it anyway would show the
            // owner a shrub the scene will never show.
            float buried = ShrubCatalog.BuriedFraction(contract, species, snow);
            if (!ShrubCatalog.IsLegalAtSnow(contract, species, snow))
                throw new ArgumentException(
                    $"'{species}' at snow '{snow}' is {buried:P0} buried — at or past the " +
                    $"contract's {ShrubCatalog.SnowLaw.BuriedCutoff:P0} cutoff the kit's ruling is: " +
                    "do not draw the shrub, draw a drift. Nothing to preview and nothing to place.");

            EnsureSpec(species, stage);

            // ONE render, through the kit's own expression — the same string the baker evaluates —
            // then the albedo read. No snow argument, exactly like the bake: snow never reaches
            // render() on this kit.
            string expr = ShrubBaker.ResultExpr(species, stage, phase, variant, frame);
            byte[] rgba = _host.EvaluateBytes($"{expr}.rgba");

            int expected = _spec.CellW * _spec.CellH * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"render came back {rgba.Length} bytes, expected {expected} for " +
                    $"{_spec.CellW}×{_spec.CellH} RGBA. The cell the rig rendered does not match the " +
                    "cell sheetSpec() promised — do not show it.");

            float depth = ShrubCatalog.SnowDepthMetres(contract, species, snow);
            RigStudioGuide[] guides = null;
            if (depth > 0f)
            {
                var hab = contract.Habitat(entry.Habitat);
                guides = new[]
                {
                    new RigStudioGuide
                    {
                        // The surface sits depth metres above the root-crown pivot, at the kit's PPU.
                        Y = _spec.PivotY - depth * ShrubCatalog.Ppu,
                        Label = $"snow surface '{snow}' — {depth:0.00} m on {entry.Habitat} ground " +
                                $"(snowK {hab?.SnowK:0.0#}), {buried:P0} buried; rows below are " +
                                "clipped at placement, the scene draws the drift",
                    },
                };
            }

            return new RigStudioPreview
            {
                Rgba = rgba,
                Width = _spec.CellW,
                Height = _spec.CellH,
                HasPivot = true,
                PivotX = _spec.PivotX,
                PivotY = _spec.PivotY,
                Guides = guides,
                Caption = $"{entry.Name} ({species}) — {entry.Habitat}, {entry.HeightM:0.0#} m " +
                          $"{entry.Unit}; {stage} · {phase} · v{variant} · sway {frame}; cell " +
                          $"{_spec.CellW}×{_spec.CellH} ({_spec.Metres:0.0#} m across)",
            };
        }

        /// <summary>
        /// Run the bake's own pre-flight for a (species, stage) and cache the resulting sheet spec:
        /// live rig geometry, the 2048 guard, and the against-the-oracle contract check — in the
        /// baker's order, via the baker's own public methods, so the preview refuses exactly where a
        /// bake would.
        /// </summary>
        void EnsureSpec(string species, string stage)
        {
            string key = species + "|" + stage;
            if (_host != null && string.Equals(_specKey, key, StringComparison.Ordinal)) return;

            if (_host == null)
            {
                _host = RigScriptHostFactory.Create();
                ShrubBaker.InstallRig(_host);
            }

            ShrubBaker.SheetSpec spec =
                ShrubBaker.ReadSheetSpec(_host, species, stage, ShrubBaker.PhaseAxis);
            ShrubBaker.AssertFits(species, stage, spec);
            ShrubBaker.AssertMatchesContract(species, spec, ShrubBaker.PhaseAxis, Contract());

            _spec = spec;
            _specKey = key;
        }

        public RigStudioBakeReport Bake(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);

            string species = RigStudioPoints.KeyOf(point, SpeciesAxis);
            string stage = RigStudioPoints.KeyOf(point, StageAxis);
            int variant = RigStudioPoints.IntOf(point, VariantAxis);

            // 1) The kit's baker, through the same public entry the menu uses — its own guard chain
            //    (fits, contract oracle) runs inside and refuses before writing anything.
            ShrubBakeResult result = ShrubBaker.BakePhaseAxis(
                new[] { species }, stage, variant,
                outputFolder: null,     // the kit's own folder — exactly where the menu bake lands
                progress: (label, t) => EditorUtility.DisplayProgressBar(
                    "Rig Studio — baking shrub sheet", label, t));

            // 2) The menu's own import discipline: the PNGs were written behind the AssetDatabase's
            //    back, so force-import them before anything reads a texture.
            AssetDatabase.Refresh();
            foreach (ShrubSheetBake sheet in result.Sheets)
                foreach (string path in sheet.AssetPaths)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // 3) The kit's slicer + verify — refusals are logged verbatim by the slicer itself and
            //    the studio window carries them to the owner.
            int sliced = ShrubSheetSlicer.SliceAll(out int failed);
            if (failed > 0)
                throw new InvalidOperationException(
                    $"The shrub slicer REFUSED {failed} sheet(s) (sliced {sliced}). Its own reasons " +
                    "are above, verbatim — nothing was guessed at.");

            if (!ShrubSheetSlicer.VerifyAll(logEachPass: false))
                throw new InvalidOperationException(
                    "The sliced shrub sheets FAILED verification against " +
                    $"{ShrubCatalog.ContractFileName} — see the mismatches above. Do not commit " +
                    "until they are clear.");

            string stem = ShrubCatalog.PhaseSheetStem(species, stage, variant);
            var minted = result.Sheets.SelectMany(s => s.AssetPaths).ToArray();
            int phases = Contract().Calendar.Phases.Count;
            int frames = Contract().Sway.Frames;

            return new RigStudioBakeReport
            {
                Minted = minted,
                PlaceableNote =
                    $"'{species}' is sliced and placeable: sprites {stem}_c0_f0 … " +
                    $"{stem}_c{phases - 1}_f{frames - 1} (phase column × sway row) under " +
                    $"{ShrubCatalog.ShrubsRoot}, root-crown pivot from the contract. NOT " +
                    "committed — this kit bakes to order, and the St Peters atlas guard names the " +
                    "only committed slice; widening it is your atlas-budget call.",
                Summary = ShrubBakeMenu.Summarise(result),
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
