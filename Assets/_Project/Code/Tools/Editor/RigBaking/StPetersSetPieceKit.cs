using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The St Peters set-pieces kit (Claude Design's key-scenes return, 2026-09-27): eight pieces for the
    /// landing and the cannery, drawn by <c>StPetersSetPieces</c> over the shared light engine.
    ///
    /// <para><b>Not a catalog rig.</b> The kit installs its own three scripts, in the order its checker
    /// loads them, rather than through a <see cref="RigCatalog"/> entry: a catalog key would need a row in
    /// a table another open PR owns. Nothing else in the repo installs these globals.</para>
    ///
    /// <para><b>Its sidecars live in the kit's OWN <c>gameplay/</c> folder.</b> Every reader of
    /// <c>*.gameplay.json</c> reads one folder, top level only, and <c>docs/art/rigs/gameplay/</c> means
    /// boat decks: a set piece there would have to parse as a deck. The kit's folder is under none of
    /// them (held by <c>StPetersSetPiecesIntakeTests</c>).</para>
    ///
    /// <para><b>Its lib files are copies</b> of two canonical files (the houses kit's
    /// <c>coastalPass.js</c>, terrain pass 9's <c>weatherSky.js</c>), committed so the kit folder stays the
    /// fourteen files that were delivered, and held byte-equal (after LF) to their canonicals by
    /// <c>StPetersSetPiecesIntakeTests</c>.</para>
    /// </summary>
    public static class StPetersSetPieceKit
    {
        public const string KitFolder = "docs/art/rigs/st-peters-set-pieces-kit";
        public const string GameplayFolder = KitFolder + "/gameplay";
        public const string GlobalName = "StPetersSetPieces";
        public const string Version = "1.0.0";
        public const string SidecarSchema = "hidden-harbours/st-peters-set-piece@1";
        public const string TodaySchema = "hidden-harbours/st-peters-set-pieces-today@1";
        public const string DefType = "SetPieceDef";

        public const int Facings = 8;
        public const int ImportSizeCap = 2048;

        /// <summary>The dir the vane's headings are rendered at: the kit places it at dir 0 so its arms
        /// and its cod are true to the compass.</summary>
        public const int HeadingRigDir = 0;

        /// <summary>The kit's scripts, kit-relative, in the order <c>checks/check-kit.cjs</c> loads them.</summary>
        public static readonly IReadOnlyList<string> Scripts = new[]
        {
            "lib/weatherSky.js",
            "lib/coastalPass.js",
            "stPetersSetPieces.js",
        };

        /// <summary>The eight pieces, rig key to Def id, in the kit's own KEYS order.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Pieces = new[]
        {
            new KeyValuePair<string, string>("slipTideBoard", "prop.stp_slip_tide_board"),
            new KeyValuePair<string, string>("harbourVane", "prop.stp_harbour_vane"),
            new KeyValuePair<string, string>("boilerStack", "structure.stp_cannery_boiler_stack"),
            new KeyValuePair<string, string>("trolleyLine", "structure.stp_cannery_trolley_line"),
            new KeyValuePair<string, string>("trolley", "prop.stp_cannery_trolley"),
            new KeyValuePair<string, string>("conveyor", "prop.stp_cannery_conveyor"),
            new KeyValuePair<string, string>("fallenSign", "prop.stp_cannery_fallen_sign"),
            new KeyValuePair<string, string>("doorNotice", "prop.stp_cannery_door_notice"),
        };

        public static string SidecarPath(string id) => $"{GameplayFolder}/{id}.gameplay.json";

        public static string Abs(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        public static string Lf(string text) => text.Replace("\r\n", "\n");

        /// <summary>SHA-256 of a text file's UTF-8 bytes after CRLF → LF, lower-case hex. The kit has no
        /// eol attribute, so a Windows checkout may hold it CRLF; the pins are the delivered LF bytes.</summary>
        public static string Sha256Lf(string repoRelative) =>
            Sha256Hex(Encoding.UTF8.GetBytes(Lf(File.ReadAllText(Abs(repoRelative)))));

        public static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>The rig dir clockwise frame k shows, through the repo's one convention helper.</summary>
        public static int RigDirForFrame(int frame) =>
            (int)RigBaker.DirForCell(frame, Facings, AzimuthConvention.CounterClockwise);

        /// <summary>
        /// Loads the kit into a host, in the checker's order, and proves the three globals and the kit
        /// version. The scripts run UNMODIFIED (ADR 0021 §5); they need no shims.
        /// </summary>
        public static void Install(IRigScriptHost host)
        {
            foreach (string script in Scripts)
                host.Execute(File.ReadAllText(Abs(KitFolder + "/" + script)));

            if (!host.EvaluateBool("typeof WeatherSky === 'object' && typeof CoastalPass === 'object' && " +
                                   $"typeof {GlobalName} === 'object'"))
                throw new InvalidOperationException(
                    $"Installing {KitFolder} left WeatherSky, CoastalPass or {GlobalName} undefined.");

            string version = host.EvaluateString($"{GlobalName}.VERSION");
            if (version != Version)
                throw new InvalidOperationException(
                    $"{GlobalName} is {version}; this intake was proven on {Version}. A new kit version is a " +
                    "new intake: re-run the checker and regenerate the today file.");
        }

        /// <summary>The today file's text: the options each piece is baked at.</summary>
        public static string ReadTodayText() => File.ReadAllText(Abs(SetPieceSheetSlicer.TodayPath));

        /// <summary>
        /// Refuses a today file that is not this kit's: wrong schema, another global or version, or kit
        /// scripts whose LF bytes are not the ones it was generated against.
        /// </summary>
        public static void CheckToday(object today)
        {
            if (DeckSidecarJson.String(DeckSidecarJson.Member(today, "schema")) != TodaySchema)
                throw new InvalidOperationException($"{SetPieceSheetSlicer.TodayPath} is not a {TodaySchema} document.");

            object kit = DeckSidecarJson.Member(today, "kit");
            if (DeckSidecarJson.String(DeckSidecarJson.Member(kit, "folder")) != KitFolder ||
                DeckSidecarJson.String(DeckSidecarJson.Member(kit, "global")) != GlobalName ||
                DeckSidecarJson.String(DeckSidecarJson.Member(kit, "version")) != Version)
                throw new InvalidOperationException(
                    $"{SetPieceSheetSlicer.TodayPath} was generated for another kit (folder, global or version).");

            var scripts = DeckSidecarJson.AsArray(DeckSidecarJson.Member(kit, "scripts"));
            if (scripts == null || scripts.Count != Scripts.Count)
                throw new InvalidOperationException($"{SetPieceSheetSlicer.TodayPath} does not list the kit's {Scripts.Count} scripts.");

            for (int i = 0; i < Scripts.Count; i++)
            {
                string path = DeckSidecarJson.String(DeckSidecarJson.Member(scripts[i], "path"));
                string pinned = DeckSidecarJson.String(DeckSidecarJson.Member(scripts[i], "sha256Lf"));
                if (path != Scripts[i])
                    throw new InvalidOperationException($"today script {i} is '{path}', expected '{Scripts[i]}'.");
                string actual = Sha256Lf(KitFolder + "/" + path);
                if (actual != pinned)
                    throw new InvalidOperationException(
                        $"{KitFolder}/{path} is {actual} after LF; the today file was generated against {pinned}.");
            }
        }
    }
}
