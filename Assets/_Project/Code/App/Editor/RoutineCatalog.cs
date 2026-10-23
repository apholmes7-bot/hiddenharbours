using System;
using System.Linq;
using HiddenHarbours.World;
using UnityEditor;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The shared editor catalog: each region reads its own days.</summary>
    public static class RoutineCatalog
    {
        public const string Folder = "Assets/_Project/Data/Routines";

        public static RoutineDef[] LoadRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return Array.Empty<RoutineDef>();
            return AssetDatabase.FindAssets("t:RoutineDef", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<RoutineDef>)
                .Where(r => r != null && string.Equals(r.RegionId, regionId, StringComparison.Ordinal))
                .OrderBy(r => r.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
