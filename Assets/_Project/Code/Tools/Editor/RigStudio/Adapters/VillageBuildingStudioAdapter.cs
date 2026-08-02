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
    /// The VILLAGE BUILDING KIT's Rig Studio adapter — the first of the per-kit adapters, and the one
    /// that retires the old Building Studio's defect class for this family: options like
    /// <c>{preset:'x'}</c> have SILENTLY DONE NOTHING here (both rigs resolve unknown keys to a
    /// default without a word). A live preview through the bake's own render path makes that failure
    /// visible by construction, and the preview==bake bit-identity test makes it structural.
    ///
    /// <para><b>Axes.</b> <c>build</c> — the kit's own <see cref="VillageBuildingKit.M1Set"/> keys,
    /// enumerated from the kit, never restated; <c>facing</c> — <c>0..Facings−1</c> from the kit's
    /// own constant. (The M1 set is a fixed, canon-pinned table — free-dialling every rig axis is
    /// the old Building Studio's job and stays there; this kit's parameter space IS its build
    /// table.)</para>
    ///
    /// <para><b>Preview.</b> One facing of <see cref="BuildingRigBaker.RenderCells"/> — THE render
    /// path, the same call the bake packs into a sheet, union crop, measured convention and all.
    /// Facing <c>i</c> therefore genuinely depicts +45°·i, exactly like the baked sheet (unlike the
    /// old Building Studio, which shows raw rig cells and needs a banner to explain itself).</para>
    ///
    /// <para><b>Bake.</b> Delegates to the kit's own menu chain — <see cref="VillageBuildingBakeMenu"/>
    /// (all five builds + the contract: the contract is written whole, so the kit's atomic bake unit
    /// is the set), then <see cref="VillageBuildingSheetSlicer"/>, then
    /// <see cref="VillageBuildingPrefabBuilder"/> into the placeable pool. Output lands in the
    /// working tree exactly where the menu bake puts it; nothing is committed.</para>
    /// </summary>
    public sealed class VillageBuildingStudioAdapter : IRigStudioAdapter
    {
        public const string BuildAxis = "build";
        public const string FacingAxis = "facing";

        // The live rig host and the last build's rendered cell set are kept between previews:
        // scrubbing the facing axis is the loop this window exists for, and it must cost a memory
        // read, not eight fresh renders.
        IRigScriptHost _host;
        BuildingCellSet _cells;
        string _cellsBuildKey;

        RigStudioSchema _schema;

        public RigStudioSchema Describe()
        {
            if (_schema != null) return _schema;

            // The kit's own build table, in the kit's own order — the order a village reads in.
            string[] buildKeys = VillageBuildingKit.M1Set.Select(b => b.Key).ToArray();

            _schema = new RigStudioSchema
            {
                KitName = "Village buildings",
                About = "The M1 village set — a school, the general store and three clapboard " +
                        "houses, baked from the two parametric building rigs.",
                BakeNote = "Bake this re-bakes the kit's WHOLE M1 set (its contract is one file, " +
                           "written whole), slices the sheets and rebuilds the placeable prefabs — " +
                           "the same three menu steps, in order, from one button.",
                Axes = new[]
                {
                    new RigStudioAxis
                    {
                        Key = BuildAxis,
                        Label = "Building",
                        Kind = RigStudioAxisKind.Keys,
                        Keys = buildKeys,
                        Default = buildKeys[0],
                    },
                    new RigStudioAxis
                    {
                        Key = FacingAxis,
                        Label = "Facing",
                        Kind = RigStudioAxisKind.IntRange,
                        Min = 0,
                        Max = VillageBuildingKit.Facings - 1,
                        Default = 0,
                        Note = "facing i depicts +45°·i, as baked (0 is the building's BACK — " +
                               "both rigs put the door on the far gable)",
                    },
                },
            };
            return _schema;
        }

        public RigStudioPreview Preview(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);

            string buildKey = RigStudioPoints.KeyOf(point, BuildAxis);
            int facing = RigStudioPoints.IntOf(point, FacingAxis);

            VillageBuildingKit.Build? found = VillageBuildingKit.FindBuild(buildKey);
            if (found == null)
                throw new ArgumentException(
                    $"'{buildKey}' passed the axis check but VillageBuildingKit.M1Set does not carry " +
                    "it — the adapter's schema is lying about the kit. Refusing to render anything " +
                    "in its place.");
            VillageBuildingKit.Build build = found.Value;

            EnsureRendered(build);

            return new RigStudioPreview
            {
                Rgba = _cells.CroppedCell(facing),
                Width = _cells.CellWidth,
                Height = _cells.CellHeight,
                HasPivot = true,
                PivotX = _cells.PivotX,
                PivotY = _cells.PivotY,
                Caption = $"{build.Label} — {_cells.Probe.WidthMeters:0.#} × " +
                          $"{_cells.Probe.LengthMeters:0.#} m footprint, cell " +
                          $"{_cells.CellWidth}×{_cells.CellHeight}, {_cells.Probe.Convention} " +
                          "(measured; correction applied — this is the baked sheet's cell " +
                          $"{facing})",
            };
        }

        /// <summary>
        /// Render (or reuse) the build's full cell set through <see cref="BuildingRigBaker.RenderCells"/> —
        /// THE render path. Every kit guard runs on the way (preset-exists, differs-from-default,
        /// azimuth cross-check), so the preview refuses exactly where a bake would refuse.
        /// </summary>
        void EnsureRendered(VillageBuildingKit.Build build)
        {
            if (_cells != null && string.Equals(_cellsBuildKey, build.Key, StringComparison.Ordinal))
                return;

            _host ??= RigScriptHostFactory.Create();

            // The kit's own request — the same options expression, cap and tripwires a menu bake
            // hands the baker. The output folder is irrelevant here (nothing is written), but it is
            // the kit's real one so the request is the genuine article.
            BuildingBakeRequest request = VillageBuildingBakeMenu.RequestFor(
                build, VillageBuildingBakeMenu.OutputFolder, VillageBuildingKit.StemFor(build.Key));

            _cells = BuildingRigBaker.RenderCells(request, _host);
            _cellsBuildKey = build.Key;
        }

        public RigStudioBakeReport Bake(IReadOnlyDictionary<string, object> point)
        {
            RigStudioPoints.Validate(Describe(), point);
            string buildKey = RigStudioPoints.KeyOf(point, BuildAxis);
            VillageBuildingKit.Build build = VillageBuildingKit.FindBuild(buildKey)
                ?? throw new ArgumentException($"'{buildKey}' is not in VillageBuildingKit.M1Set.");

            // 1) The kit's bake — all five builds + the contract, exactly the menu command.
            string bakeLog = VillageBuildingBakeMenu.BakeAll(out int failed);
            AssetDatabase.Refresh();
            if (failed > 0)
                throw new InvalidOperationException(
                    $"{failed} build(s) FAILED — nothing was written for them and the contract " +
                    $"covers only what succeeded. The kit's own report:\n{bakeLog}");

            // 2) The kit's slicer. Refusals were logged verbatim by the slicer itself; this throw
            //    carries the count and points at them rather than paraphrasing.
            int sliced = VillageBuildingSheetSlicer.SliceAll(out int sliceFailed);
            if (sliceFailed > 0)
                throw new InvalidOperationException(
                    $"The slicer REFUSED {sliceFailed} sheet(s) (sliced {sliced}). Its own reasons " +
                    "are above, verbatim — nothing was guessed at and nothing was sliced on a " +
                    "plausible default.");

            // 3) The placeable pool — the kit's prefab builder, into the kit's own folder.
            int prefabs = VillageBuildingPrefabBuilder.BuildAll(VillageBuildingCatalog.PrefabRoot);
            if (prefabs == 0)
                throw new InvalidOperationException(
                    "The bake and slice succeeded but no prefab was built — the placeable pool is " +
                    "unchanged. See the prefab builder's warnings above.");

            VillageBuildingKit.Contract contract = VillageBuildingKit.Load();
            string stem = VillageBuildingKit.StemFor(build.Key);

            var minted = new List<string>(VillageBuildingKit.AllSheetPaths(contract))
            {
                VillageBuildingKit.ContractPath,
                $"{VillageBuildingCatalog.PrefabRoot}/ ({prefabs} prefab(s))",
            };

            return new RigStudioBakeReport
            {
                Minted = minted.ToArray(),
                PlaceableNote =
                    $"'{build.Label}' is now placeable: drag " +
                    $"{VillageBuildingCatalog.PrefabRoot}/{stem}.prefab into a region scene " +
                    "(it stands on its ground line, door toward the camera), or run " +
                    "Hidden Harbours ▸ Dev ▸ Place Village Lineup to see the whole set.",
                Summary = bakeLog,
            };
        }

        public void Dispose()
        {
            _host?.Dispose();
            _host = null;
            _cells = null;
            _cellsBuildKey = null;
        }
    }
}
#endif
