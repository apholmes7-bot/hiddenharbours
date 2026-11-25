using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Loads the shipped island-supply Defs, the shipped GameConfig and the island region's real tide
    /// for the island-supply tests. Everything is read from the assets; nothing here is a copy of a number.</summary>
    internal static class IslandSupplyTestData
    {
        public const string DataRoot = "Assets/_Project/Data";
        public const string SupplyRoot = "Assets/_Project/Data/Resources/IslandSupply/";
        public const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        public static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { DataRoot }))
            {
                var def = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) list.Add(def);
            }
            return list;
        }

        public static GameConfig Config()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (cfg == null) throw new InvalidOperationException("GameConfig asset missing at " + ConfigPath);
            return cfg;
        }

        public static IslandSupplyConfig Route()
        {
            var route = Resources.Load<IslandSupplyConfig>(IslandSupplyConfig.ResourcesPath);
            if (route == null) throw new InvalidOperationException("IslandSupplyConfig missing at Resources/" + IslandSupplyConfig.ResourcesPath);
            return route;
        }

        public static RegionDef Region(string id)
        {
            foreach (RegionDef r in LoadAll<RegionDef>())
                if (r.Id == id) return r;
            throw new InvalidOperationException("no RegionDef with id " + id);
        }

        /// <summary>The island's real water level: <see cref="TideModel.Height"/> with the region asset's own
        /// tide profile and the shipped GameConfig.</summary>
        public static Func<double, float> Water(IslandSupplyConfig route, GameConfig cfg)
        {
            RegionDef r = Region(route.IslandRegionId);
            var profile = new TideProfile { MeanLevel = r.TideMeanLevel, Amplitude = r.TideAmplitude, PhaseHours = r.TidePhaseHours };
            return t => TideModel.Height(t, profile, cfg);
        }

        public static T ById<T>(IEnumerable<T> all, Func<T, string> id, string want) where T : UnityEngine.Object
        {
            foreach (T d in all)
                if (id(d) == want) return d;
            throw new InvalidOperationException("no " + typeof(T).Name + " with id " + want);
        }

        public static Dictionary<string, CarrierDef> CarriersById()
        {
            var map = new Dictionary<string, CarrierDef>(StringComparer.Ordinal);
            foreach (CarrierDef c in LoadAll<CarrierDef>()) map[c.Id] = c;
            return map;
        }
    }
}
