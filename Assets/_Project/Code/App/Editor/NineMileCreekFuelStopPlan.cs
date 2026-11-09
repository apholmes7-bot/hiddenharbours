#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Validated inputs for H9's named scene patch; planning never changes the scene.</summary>
    public static class NineMileCreekFuelStopPlan
    {
        public const string ForecourtParent = "CreekFuel";
        public const string WharfParent = "NmcWharfLandSide";
        static readonly Dictionary<string, string> Shared = new Dictionary<string, string>
        {
            { "wheelStop", "prop.nmc_wheel_stop" }, { "stopSign", "prop.nmc_stop_sign" },
            { "lampPole", "prop.nmc_lamp_pole" }, { "picnic", "prop.nmc_picnic" },
            { "bin", "prop.nmc_bin" }, { "iceBox", "prop.nmc_ice_box" }, { "dumpster", "prop.nmc_dumpster" },
        };
        public sealed class Placement
        {
            public KeyScenePiece Row;
            public SetPieceDef Definition;
            public string Parent;
            public string Name => Row.Id;
        }
        public static string DefinitionId(KeyScenePiece row) => Shared.TryGetValue(row.Piece, out string id) ? id : row.Id;

        public static IReadOnlyList<Placement> Build(KeySceneDef forecourt, KeySceneDef wharf)
        {
            if (forecourt == null || wharf == null) throw new InvalidOperationException("Both H9 key scenes are required.");
            var result = new List<Placement>();
            foreach (var source in new[] { forecourt, wharf })
                foreach (var row in source.Pieces.Where(p => p.Kit.StartsWith("fuelStopRig", StringComparison.Ordinal)))
                {
                    string path = "Assets/_Project/Data/SetPieces/SetPiece_" + row.Piece + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(path);
                    if (def == null || def.Id != DefinitionId(row) || def.RigKey != row.Piece || !def.HasArt)
                        throw new InvalidOperationException(row.Id + ": missing, unsliced or mismatched fuel stop Def at " + path);
                    result.Add(new Placement { Row = row, Definition = def,
                        Parent = source == forecourt ? ForecourtParent : WharfParent });
                }
            if (result.Count != 39 || result.Select(p => p.Name).Distinct().Count() != 39)
                throw new InvalidOperationException("H9 requires 39 unique fuelStopRig placements (28 forecourt, eleven wharf).");
            if (result.Count(p => p.Parent == ForecourtParent) != 28)
                throw new InvalidOperationException("The fuel stop forecourt must have 28 set pieces.");
            return result;
        }
    }
}
#endif
