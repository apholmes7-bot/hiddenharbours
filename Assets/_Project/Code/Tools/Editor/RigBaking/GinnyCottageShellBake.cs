using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Ginny's cottage, baked on its own.</b> Her house started as the village sage cottage, but it has
    /// three real rooms below, a corridor and two bedrooms above and a stair along the north wall, and
    /// that does not fit the sage cottage's 6.6 × 8.05 m footprint. So her shell is the same dialled house
    /// — same siding, colour, roof, porch and weather — at a larger size, with the old cottage's
    /// three-window front kept (<see cref="OldFrontDials"/>) and the details from her artboard added
    /// (<see cref="ArtboardDetails"/>), baked into its own folder so the village sage cottage (and the
    /// kit's contract, which claims every sheet under the village folder) never move.
    ///
    /// <para>The sheet is sliced with the kit's own slicer and import lock against the entry this bake
    /// produced, so it imports exactly as a kit sheet does. Re-run it from the menu whenever the house
    /// rig changes; nothing else writes these sheets.</para>
    /// </summary>
    public static class GinnyCottageShellBake
    {
        /// <summary>The build key. Also the stem root: <c>Village_ginnyCottage*.png</c>.</summary>
        public const string Key = "ginnyCottage";

        /// <summary>The kit build Ginny's house is dialled from.</summary>
        public const string BasedOn = "sageCottage";

        /// <summary>The house rig's size dial for her cottage. 0.5 gives a 7.2 × 9.1 m footprint: the
        /// least that fits a stair along an outside wall, a washroom deep enough to stand in and an
        /// upstairs corridor that the stair arrives in.</summary>
        public const double SizeDial = 0.5;

        /// <summary>Where her sheets live — outside the village kit's folder on purpose.</summary>
        public const string Folder = "Assets/_Project/Art/Sprites/Buildings/Ginny";

        /// <summary>Her build: the sage cottage's dialled options with only the size changed.</summary>
        public static VillageBuildingKit.Build Build()
        {
            VillageBuildingKit.Build sage = default;
            bool found = false;
            foreach (VillageBuildingKit.Build b in VillageBuildingKit.AllBuilds)
                if (b.Key == BasedOn) { sage = b; found = true; break; }
            if (!found || sage.Dialled == null)
                throw new InvalidOperationException($"'{BasedOn}' is not a dialled build in the village kit.");

            var dialled = new Dictionary<string, object>();
            foreach (var kv in sage.Dialled) dialled[kv.Key] = kv.Value;
            dialled["size"] = SizeDial;
            foreach (var kv in OldFrontDials) dialled[kv.Key] = kv.Value;
            foreach (var kv in ArtboardDetails) dialled[kv.Key] = kv.Value;
            return VillageBuildingKit.Build.FromDialled(Key, "Ginny's cottage", sage.RigKey, dialled,
                "the sage cottage enlarged for Ginny's rooms, with the old three-window front and the artboard's details");
        }

        /// <summary>
        /// <b>The old front on the larger shell</b> (owner 2026-11-22: "i prefer the old three window look").
        /// At <see cref="SizeDial"/> the rig's own density puts four windows on each eave wall and, because the
        /// wall is over the rig's 4.4 m second-storey line, a second row above them. A density of 0.40 gives
        /// three on the 9.1 m wall, as the sage cottage has on its 8.05 m one, and <c>upperWindows: false</c>
        /// keeps the eave walls to that one row; the storey above keeps its gable windows.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, object> OldFrontDials = new Dictionary<string, object>
        {
            ["winDensity"] = 0.40,
            ["upperWindows"] = false,
        };

        /// <summary>
        /// <b>The details from Ginny's cottage artboard</b> (the key scenes' return 3, the cottage, 2026-09-28:
        /// <c>ginnyCottage.dial.json</c>, ruled by the owner that night). Every detail is taken. The artboard's
        /// yellow body and lighter weathering are NOT: the owner keeps the old sage look, and the artboard's own
        /// notes say "for the plot's sage colour, set body: sage; every detail stays". Its door is red, which is
        /// what the rig already picks for a sage body. Its sea-green shutters were chosen to sit with the
        /// yellow, so the old cottage's joinery-green shutters stay.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, object> ArtboardDetails = new Dictionary<string, object>
        {
            ["doorPaint"] = "red",
            ["windowBoxes"] = true,
            ["wallBench"] = true,
            ["stovepipe"] = true,
            ["roofPatches"] = 5,
            ["roofLadder"] = true,
        };

        [MenuItem("Hidden Harbours/Art/Bake Ginny's Cottage Shell", priority = 203)]
        public static void BakeMenu() => Debug.Log(Bake());

        /// <summary>Headless entry point. Exits non-zero on any failure.</summary>
        public static void BakeFromCommandLine()
        {
            try { Debug.Log(Bake()); EditorApplication.Exit(0); }
            catch (Exception e)
            {
                Debug.LogError($"[ginny-cottage] bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Bake, then slice every sheet of the build against the entry the bake produced.</summary>
        public static string Bake()
        {
            VillageBuildingKit.Build build = Build();
            Directory.CreateDirectory(Folder);
            string stem = VillageBuildingKit.StemFor(Key);
            BuildingBakeResult r = BuildingRigBaker.Bake(VillageBuildingBakeMenu.RequestFor(build, Folder, stem));
            VillageBuildingKit.Entry entry = VillageBuildingBakeMenu.ToEntry(build, r);
            AssetDatabase.Refresh();

            var stems = new List<string> { stem };
            if (VillageBuildingKit.HasLightChannels(entry))
                foreach (VillageBuildingKit.Channel c in VillageBuildingKit.LightChannels)
                    stems.Add(VillageBuildingKit.StemFor(Key, c));
            foreach (string s in stems)
            {
                string path = $"{Folder}/{s}.png";
                if (!VillageBuildingSheetSlicer.SliceOne(path, s, entry))
                    throw new InvalidOperationException($"slicing '{path}' failed — see the errors above.");
            }
            AssetDatabase.SaveAssets();
            return $"[ginny-cottage] baked {stem}: {entry.sheetW}×{entry.sheetH}, footprint " +
                   $"{entry.footprintWidthMetres:0.##} × {entry.footprintLengthMetres:0.##} m, " +
                   $"pivot ({entry.unityPivotX:0.####}, {entry.unityPivotY:0.####}), {stems.Count} sheet(s) sliced.";
        }
    }
}
