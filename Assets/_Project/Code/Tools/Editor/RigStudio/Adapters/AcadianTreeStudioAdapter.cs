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
    /// The ACADIAN TREE KIT's Rig Studio adapter — thin by design, third in after buildings and
    /// shrubs proved the seam. Axes from the kit's committed <c>Trees.json</c>: species × stage ×
    /// season × variant (one sway row is baked on this kit — the wind shader owns the swaying — so
    /// there is no frame axis to offer).
    ///
    /// <para><b>⭐ TAMARACK SHOWS AS EXCLUDED, NOT BAKEABLE.</b> The hold-back is the kit's own:
    /// Tamarack fails the pass-2 rig's rule-1 gate (thinPct 5.4 % against the 4 % ceiling), and the
    /// coordinator ruling of 2026-07-29 is to ship the nine improved species, hold Tamarack at its
    /// pass-1 bake, and touch neither the rig nor the gate. So the studio greys it with that reason
    /// rather than hiding it (hidden, it reads as missing art) or offering it (offered, the owner
    /// approves art the kit's own audit rejects). <see cref="TreeKitCatalog.HeldBackSpecies"/> is the
    /// mechanism; the generic excluded-value test arms against this adapter automatically.</para>
    ///
    /// <para><b>Preview</b> renders through the baker's own <see cref="TreeRigBaker.ResultExpr"/> +
    /// <see cref="TreeRigBaker.ChannelExpr"/> (albedo read), after the baker's own
    /// <see cref="TreeRigBaker.AssertFits"/>. This kit's contract is written BY the bake (serializer
    /// == parser), so there is no oracle refusal to mirror — the live rig is what a bake produces,
    /// and the bit-identity pin holds against exactly that.</para>
    ///
    /// <para><b>Bake</b> is the kit's two menu commands in order: the full nine-species bake (the
    /// contract is one file, written whole — a single-species bake would shrink it and orphan the
    /// other eight's committed sheets), the slicer + verify, then the prefab builder into the
    /// placeable pool. The tree sheets ARE committed art, pinned bit-exact against a fresh render by
    /// <c>TreeSheetImportTests</c> — so an unchanged rig re-bakes byte-identical and the working tree
    /// stays clean, while a drifted rig shows up as a real diff to review.</para>
    /// </summary>
    public sealed class AcadianTreeStudioAdapter : IRigStudioAdapter
    {
        public const string SpeciesAxis = "species";
        public const string StageAxis = "stage";
        public const string SeasonAxis = "season";
        public const string VariantAxis = "variant";

        IRigScriptHost _host;
        TreeKitCatalog.Contract _contract;
        RigStudioSchema _schema;

        TreeRigBaker.SheetSpec _spec;
        string _specKey;

        TreeKitCatalog.Contract Contract()
        {
            if (_contract != null) return _contract;
            _contract = TreeKitCatalog.Load()
                ?? throw new InvalidOperationException(
                    $"{TreeKitCatalog.ContractPath} is missing or unreadable — it is written by the " +
                    "kit's own bake, so run Hidden Harbours ▸ Art ▸ Bake Acadian Trees first.");
            return _contract;
        }

        public RigStudioSchema Describe()
        {
            if (_schema != null) return _schema;
            TreeKitCatalog.Contract contract = Contract();

            string[] species = contract.trees.Select(e => e.species).Distinct().ToArray();
            string[] stages = contract.trees.Select(e => e.stage).Distinct().ToArray();
            string[] seasons = contract.trees.SelectMany(e => e.seasons).Distinct().ToArray();

            // The kit's own hold-back list, with the kit's own reason — shown greyed, never hidden
            // and never bakeable. See the class remarks for the measurement and the ruling.
            var excluded = TreeKitCatalog.HeldBackSpecies.ToDictionary(
                key => key,
                key => "held back at its pass-1 bake — it fails the pass-2 rig's own rule-1 audit " +
                       "(thin-strand ratio over the 4 % ceiling). Coordinator ruling 2026-07-29: " +
                       "ship the nine improved species, hold this one, do not loosen the gate. Its " +
                       "committed pass-1 sheets stay as they are.");

            _schema = new RigStudioSchema
            {
                KitName = "Acadian trees",
                About = "The nine-species Acadian forest — spruce to red oak — baked mature at " +
                        "summer; the wind shader owns the swaying.",
                BakeNote = "Bake this re-bakes the kit's WHOLE nine-species set (its contract is " +
                           "one file, written whole), slices, verifies and rebuilds the tree " +
                           "prefabs — the kit's two menu commands from one button. The tree sheets " +
                           "are committed art: an unchanged rig re-bakes byte-identical, so a diff " +
                           "after this bake is a real rig change to review.",
                Axes = new[]
                {
                    new RigStudioAxis
                    {
                        Key = SpeciesAxis, Label = "Species", Kind = RigStudioAxisKind.Keys,
                        Keys = species, Default = species[0], Excluded = excluded,
                    },
                    new RigStudioAxis
                    {
                        Key = StageAxis, Label = "Stage", Kind = RigStudioAxisKind.Keys,
                        Keys = stages, Default = TreeRigBaker.DefaultStage,
                        Note = "the kit bakes mature only today; more stages appear here the day " +
                               "the kit's contract carries them",
                    },
                    new RigStudioAxis
                    {
                        Key = SeasonAxis, Label = "Season", Kind = RigStudioAxisKind.Keys,
                        Keys = seasons, Default = TreeRigBaker.DefaultSeason,
                    },
                    new RigStudioAxis
                    {
                        Key = VariantAxis, Label = "Variant", Kind = RigStudioAxisKind.Seed,
                        Min = 0, Max = contract.sheet.cols - 1, Default = 0,
                    },
                },
            };
            return _schema;
        }

        public RigStudioPreview Preview(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);

            string species = RigStudioPoints.KeyOf(point, SpeciesAxis);
            string stage = RigStudioPoints.KeyOf(point, StageAxis);
            string season = RigStudioPoints.KeyOf(point, SeasonAxis);
            int variant = RigStudioPoints.IntOf(point, VariantAxis);

            TreeKitCatalog.Entry entry = TreeKitCatalog.Find(Contract(), species, stage)
                ?? throw new ArgumentException(
                    $"'{species}' at stage '{stage}' passed the axis check but Trees.json does not " +
                    "carry it — the adapter's schema is lying about the kit. Refusing to render " +
                    "anything in its place.");

            EnsureSpec(species, stage);

            // The baker's own expressions: one render, the albedo read — the same strings a bake
            // evaluates for this cell.
            string expr = TreeRigBaker.ChannelExpr(
                TreeRigBaker.ResultExpr(species, stage, season, variant, frame: 0),
                TreeKitCatalog.Channel.Albedo);
            byte[] rgba = _host.EvaluateBytes(expr);

            int expected = _spec.CellW * _spec.CellH * 4;
            if (rgba.Length != expected)
                throw new InvalidOperationException(
                    $"render came back {rgba.Length} bytes, expected {expected} for " +
                    $"{_spec.CellW}×{_spec.CellH} RGBA — the cell does not match what sheetSpec() " +
                    "promised. Do not show it.");

            return new RigStudioPreview
            {
                Rgba = rgba,
                Width = _spec.CellW,
                Height = _spec.CellH,
                HasPivot = true,
                PivotX = _spec.PivotX,
                // The tree pivot is a chosen ROW — pad px up from the cell floor (ADR 0026: the same
                // fraction is the wind shader's _TrunkAnchor), so the cross sits at cellH − pad.
                PivotY = _spec.CellH - _spec.Pad,
                Caption = $"{entry.name} ({species}) — {entry.metres:0.#} m {entry.form}; {stage} · " +
                          $"{season} · v{variant}; cell {_spec.CellW}×{_spec.CellH}, trunk anchor " +
                          $"{_spec.TrunkAnchor:0.###}",
            };
        }

        void EnsureSpec(string species, string stage)
        {
            string key = species + "|" + stage;
            if (_host != null && string.Equals(_specKey, key, StringComparison.Ordinal)) return;

            if (_host == null)
            {
                _host = RigScriptHostFactory.Create();
                TreeRigBaker.InstallRig(_host);
            }

            TreeRigBaker.SheetSpec spec = TreeRigBaker.ReadSheetSpec(_host, species, stage);
            TreeRigBaker.AssertFits(species, stage, spec);

            _spec = spec;
            _specKey = key;
        }

        public RigStudioBakeReport Bake(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);
            string species = RigStudioPoints.KeyOf(point, SpeciesAxis);
            string stage = RigStudioPoints.KeyOf(point, StageAxis);
            string season = RigStudioPoints.KeyOf(point, SeasonAxis);

            // 1) The kit's own full bake — null species means "the rig's set minus the held-back",
            //    which is the kit's rule, applied by the kit (it logs what it holds back).
            TreeBakeResult result = TreeRigBaker.Bake(
                species: null, stage: stage, season: season,
                progress: (label, t) => EditorUtility.DisplayProgressBar(
                    "Rig Studio — baking Acadian trees", label, t));

            // 2) The menu's import discipline, then the kit's slicer + verify.
            AssetDatabase.Refresh();
            foreach (TreeSheetBake sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(result.ContractPath, ImportAssetOptions.ForceUpdate);

            int sliced = TreeSheetSlicer.SliceAll(out int failed);
            if (failed > 0)
                throw new InvalidOperationException(
                    $"The tree slicer REFUSED {failed} sheet(s) (sliced {sliced}). Its own reasons " +
                    "are above, verbatim — nothing was guessed at.");

            if (!TreeSheetSlicer.VerifyAll(logEachPass: false))
                throw new InvalidOperationException(
                    "The sliced tree sheets FAILED verification against Trees.json — see the " +
                    "mismatches above. Do not commit until they are clear.");

            // 3) The placeable pool.
            int prefabs = AcadianTreePrefabBuilder.BuildAll(AcadianTreeCatalog.PrefabRoot);
            if (prefabs == 0)
                throw new InvalidOperationException(
                    "Bake and slice succeeded but no tree prefab was built — the placeable pool is " +
                    "unchanged. See the prefab builder's warnings above.");

            // The re-described contract may have changed (that is what a bake does on this kit).
            _contract = null;
            _schema = null;

            return new RigStudioBakeReport
            {
                Minted = result.Sheets.Select(s => s.AssetPath)
                               .Append(result.ContractPath)
                               .Append($"{AcadianTreeCatalog.PrefabRoot}/ ({prefabs} prefab(s))")
                               .ToArray(),
                PlaceableNote =
                    $"'{species}' (and the other eight) is placeable: drag its prefab from " +
                    $"{AcadianTreeCatalog.PrefabRoot}/ into a region scene, or paint with the Tree " +
                    "Paint tool. Tamarack stays held back by the kit's own audit.",
                Summary = TreeBakeMenu.Summarise(result),
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
