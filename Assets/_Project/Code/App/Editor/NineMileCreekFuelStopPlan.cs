#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

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
        /// <summary>
        /// <b>What a lit kind lights on the ground</b> (ADR 0016; the owner's 2026-09-04 ruling: the bloom is the
        /// FITTING, the ground light is the pool). The kit's light rule names which kinds are lit; this table says
        /// what each one is as a lamp, from the rig's own drawing (docs/art/rigs/fuel-stop/fuelStopRig.js):
        /// <list type="bullet">
        /// <item>lampPole: the one drawn luminaire, the head box 'pole.head' (0.68 m wide, underside at 7.62 m, on the
        /// arm 1.415 m off the post). A flood, bloom at the head, pool below it.</item>
        /// <item>canopy and the semi lane: no drawn soffit fitting (their emitters are fascia neon and pump faces), so
        /// no bloom; a flood's pool under the deck at each machine, which is where the kit's night frames would light
        /// the ground.</item>
        /// <item>store: the 'entry.light' fittings over the door at 2.96 m. A worklight's pool at the door, no bloom
        /// (the fitting is under the awning and is not seen from above).</item>
        /// </list>
        /// The pylon and the ice box are SIGNS: their light is neon and LED on their own faces, which needs the
        /// kit's night frames, and the export carries none. They throw no pool and are given no disc.
        /// </summary>
        public sealed class LampRule
        {
            public string Piece;
            public LightPresets.Kind Kind;
            /// <summary>Width of the drawn fitting the bloom is sized to, metres; 0 = no bloom (pool only).</summary>
            public float FittingWidth;
            /// <summary>The fitting's height over the piece's ground, metres; 0 = the piece's own top.</summary>
            public float Height;
            /// <summary>The fitting's place in piece metres when it is not at an anchor or a machine.</summary>
            public Vector2 Local;
            /// <summary>An anchor of the Def the light stands at, or null.</summary>
            public string Anchor;
            /// <summary>One light per pump machine of the row (at the machine's nozzles), not one for the piece.</summary>
            public bool PerMachine;
        }

        public static readonly LampRule[] LampRules =
        {
            new LampRule { Piece = "lampPole", Kind = LightPresets.Kind.Floodlight, FittingWidth = 0.68f, Height = 7.62f, Local = new Vector2(0f, 1.415f) },
            new LampRule { Piece = "canopy", Kind = LightPresets.Kind.Floodlight, PerMachine = true },
            new LampRule { Piece = "truck", Kind = LightPresets.Kind.Floodlight, PerMachine = true },
            new LampRule { Piece = "store", Kind = LightPresets.Kind.Worklight, Height = 2.96f, Anchor = "door" },
        };

        public static LampRule LampRuleFor(string piece) => LampRules.FirstOrDefault(r => r.Piece == piece);

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
