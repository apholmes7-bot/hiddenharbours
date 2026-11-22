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
    /// — same siding, colour, roof, porch, windows and weather — at a larger size, baked into its own
    /// folder so the village sage cottage (and the kit's contract, which claims every sheet under the
    /// village folder) never move.
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
            return VillageBuildingKit.Build.FromDialled(Key, "Ginny's cottage", sage.RigKey, dialled,
                "the sage cottage enlarged for Ginny's rooms");
        }

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
