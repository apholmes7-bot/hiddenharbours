using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    public static class NmcSetPieceKit
    {
        public const string KitFolder = "docs/art/rigs/nmc-set-pieces-kit";
        public const string GameplayFolder = KitFolder + "/gameplay";
        public const string GlobalName = "NmcSetPieces";
        public const string Version = "1.4.0";
        public const string SidecarSchema = "hidden-harbours/nmc-set-piece@1";
        public const string TodaySchema = "hidden-harbours/nmc-set-pieces-today@1";
        public const string SheetFolder = "Assets/_Project/Art/Sprites/NineMileCreek/SetPieces";
        public const string TodayPath = SheetFolder + "/nmcSetPieces.today.json";
        public const string ContractPath = SheetFolder + "/nmcSetPieces.contract.json";
        public const string ContractSchema = "hidden-harbours/nmc-set-pieces-contract@1";
        public const int ImportSizeCap = 4096;
        public const int Facings = 8;
        public static readonly IReadOnlyList<string> Scripts = new[]
        {
            "lib/weatherSky.js", "lib/coastalPass.js", "nmcSetPieces.js",
        };
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Pieces = new[]
        {
            new KeyValuePair<string, string>("officeTrailer", "structure.nmc_wharf_office"),
            new KeyValuePair<string, string>("wharfCrane", "structure.nmc_wharf_crane"),
            new KeyValuePair<string, string>("harbourLight", "structure.nmc_harbour_light"),
            new KeyValuePair<string, string>("restaurant", "structure.nmc_restaurant"),
            new KeyValuePair<string, string>("fishMarket", "structure.nmc_fish_market"),
            new KeyValuePair<string, string>("landingPad", "prop.nmc_crane_pad"),
        };
        public static readonly HashSet<string> ExtraGameplayMembers = new HashSet<string>(StringComparer.Ordinal) { "crane" };
        public static readonly HashSet<string> ExtraAnchorMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "state", "note", "size", "role", "vendor", "deckZ", "patio", "colour",
            "slewDeg", "reachM", "hookZ", "luffDeg", "jibDeg", "load",
        };
        public static readonly SetPieceKit Definition = new SetPieceKit
        {
            Folder = KitFolder, GlobalName = GlobalName, Version = Version, SidecarSchema = SidecarSchema,
            TodaySchema = TodaySchema, Scripts = Scripts, Pieces = Pieces,
            ExtraGameplayMembers = ExtraGameplayMembers, ExtraAnchorMembers = ExtraAnchorMembers,
            ImportSizeCap = ImportSizeCap, Facings = Facings,
            Sheets = new SetPieceSheetSlicer.Kit(SheetFolder, TodayPath, ContractPath, ContractSchema),
        };
        public static void Install(IRigScriptHost host) => Definition.Install(host);
        public static string ReadTodayText() => Definition.ReadTodayText();
        public static void CheckToday(object today) => Definition.CheckToday(today);
    }

    /// <summary>One kit's inputs to the shared set-piece route. It declares the extra fields retained as JSON.</summary>
    public sealed class SetPieceKit
    {
        public string Folder, GlobalName, Version, SidecarSchema, TodaySchema;
        public int ImportSizeCap, Facings;
        public IReadOnlyList<string> Scripts;
        public IReadOnlyList<KeyValuePair<string, string>> Pieces;
        public HashSet<string> ExtraGameplayMembers, ExtraAnchorMembers;
        public SetPieceSheetSlicer.Kit Sheets;
        public string ReadTodayText() => File.ReadAllText(StPetersSetPieceKit.Abs(Sheets.TodayPath));

        public void Install(IRigScriptHost host)
        {
            foreach (string script in Scripts)
                host.Execute(File.ReadAllText(StPetersSetPieceKit.Abs(Folder + "/" + script)));
            if (!host.EvaluateBool("typeof WeatherSky === 'object' && typeof CoastalPass === 'object' && " +
                                   $"typeof {GlobalName} === 'object'"))
                throw new InvalidOperationException($"Installing {Folder} left a required global undefined.");
            if (host.EvaluateString($"{GlobalName}.VERSION") != Version)
                throw new InvalidOperationException($"{GlobalName}: expected kit version {Version}.");
        }

        public void CheckToday(object today)
        {
            string Str(object o, string k) => DeckSidecarJson.String(DeckSidecarJson.Member(o, k));
            if (Str(today, "schema") != TodaySchema)
                throw new InvalidOperationException($"{Sheets.TodayPath} is not a {TodaySchema} document.");
            object kit = DeckSidecarJson.Member(today, "kit");
            if (Str(kit, "folder") != Folder || Str(kit, "global") != GlobalName || Str(kit, "version") != Version)
                throw new InvalidOperationException($"{Sheets.TodayPath} was generated for another kit.");
            var scripts = DeckSidecarJson.AsArray(DeckSidecarJson.Member(kit, "scripts"));
            if (scripts == null || scripts.Count != Scripts.Count)
                throw new InvalidOperationException("today does not list the kit scripts.");
            for (int i = 0; i < Scripts.Count; i++)
                if (Str(scripts[i], "path") != Scripts[i] ||
                    Str(scripts[i], "sha256Lf") != StPetersSetPieceKit.Sha256Lf(Folder + "/" + Scripts[i]))
                    throw new InvalidOperationException($"today script {i} differs from the kit's pinned LF bytes.");
        }
    }
}
