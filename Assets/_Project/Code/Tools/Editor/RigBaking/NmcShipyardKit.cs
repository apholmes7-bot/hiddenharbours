using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>The whole delivered yard as its thirteen independently sorted SetPieceDefs.</summary>
    public static class NmcShipyardKit
    {
        public const string Folder = "docs/art/rigs/nmc-shipyard-kit";
        public const string SheetFolder = "Assets/_Project/Art/Sprites/NineMileCreek/Yard";
        public const string TodayPath = SheetFolder + "/nmcShipyard.today.json";
        public const int ImportSizeCap = 4096, Facings = 8;
        public static readonly string[] Keys = { "yardFront", "pierW", "pierE", "lift", "slab", "hardA", "hardB", "hardC", "shed", "floodW", "floodE", "fence", "props" };
        public static readonly SetPieceKit Definition = new SetPieceKit
        {
            Folder = Folder, GlobalName = "NmcShipyard", Version = "1.0.0",
            SidecarSchema = "hidden-harbours/nmc-shipyard-part@1", TodaySchema = "hidden-harbours/nmc-shipyard-today@1",
            ImportSizeCap = ImportSizeCap, Facings = Facings,
            Scripts = new[] { "../nmc-set-pieces-kit/lib/weatherSky.js", "../nmc-set-pieces-kit/lib/coastalPass.js",
                "wharfRig2.geo.js", "wharfRig2.fam.js", "wharfRig2.verbs.js", "wharfRig2.js", "wharfRig2.kit.js", "marinaRig.js",
                "bowriderIsoRig.js", "sportSkiffIsoRig.js", "lobsterBoatVariantsIsoRig.js", "capeIslanderIsoRig.js", "marinaBoats.js",
                "shipyardRig2.js", "nmcWharf.js", "yard-layout.js", "nmcShipyard.js" },
            Pieces = Keys.Select(k => new KeyValuePair<string, string>(k, IdFor(k))).ToArray(),
            ExtraGameplayMembers = new HashSet<string>(StringComparer.Ordinal) { "placement", "walkSurfaces", "records" },
            ExtraAnchorMembers = new HashSet<string>(StringComparer.Ordinal),
            Sheets = new SetPieceSheetSlicer.Kit(SheetFolder, TodayPath, SheetFolder + "/nmcShipyard.contract.json", "hidden-harbours/nmc-shipyard-contract@1"),
        };
        public static string IdFor(string key) => "structure.nmc_yard_" + Regex.Replace(key, "[A-Z]", m => "_" + m.Value.ToLowerInvariant());
        public static void BakeFromCommandLine()
        {
            var problems = StPetersSetPieceBaker.BakeSliceBuild(Definition);
            if (problems.Count != 0) throw new InvalidOperationException(string.Join("\n", problems));
        }
    }
}
