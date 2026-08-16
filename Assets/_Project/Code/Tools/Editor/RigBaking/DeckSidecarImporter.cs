using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Sidecar → Def, with no human copy step (M2-37, data half).</b> Reads every
    /// <c>docs/art/rigs/gameplay/&lt;rig&gt;.gameplay.json</c>, checks each against the rig it was cut
    /// from, and writes the walkable polygons, the washboard strips and the named cleats onto a
    /// <see cref="BoatDeckDef"/> asset per hull — then points that hull's
    /// <see cref="BoatVisualDef.Deck"/> at it. The runtime reads the asset; nobody types a coordinate.
    ///
    /// <para>That "nobody types a coordinate" is the whole point of the design ruling this executes
    /// (<c>docs/design/deck-boarding-cleats-and-interact-capture.md</c> §2): the baked anchors JSON went
    /// dead because the runtime's real motor mount was a hand-transcribed constant beside it, and the
    /// two drifted. Re-running this menu item is the only supported way to change a deck polygon.</para>
    ///
    /// <para><b>The one table here is WIRING, not geometry.</b> <see cref="VisualsBySidecar"/> says
    /// which visual assets wear which sidecar's deck — the join a <see cref="BoatVisualDef"/> cannot
    /// make for itself (nothing on it names a rig, and two paint builds of one hull share one deck). Not
    /// a single number in it comes from the sidecars.</para>
    ///
    /// <para><b>A sidecar names its HULL; the rig is read from inside it.</b> The eleven original files
    /// are named after their rig, which is only possible while a rig makes one boat. The lobster
    /// generator makes eighteen, so those files are named after the hull and the rig comes from the
    /// sidecar's own <c>rig</c> field — see <see cref="DeckSidecarReader.ResolveRigFileName"/>, which
    /// also explains why reading the field is the safer source even for the eleven.</para>
    /// </summary>
    public static class DeckSidecarImporter
    {
        const string SidecarFolder = "docs/art/rigs/gameplay";
        const string RigFolder = "docs/art/rigs";
        const string DeckFolder = "Assets/_Project/Data/Boats/Decks";
        const string VisualsFolder = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>
        /// Which <c>Data/Boats/Visuals</c> assets wear each rig's deck. A WIRING manifest, in the same
        /// spirit as <c>BoatVisualLibraryBuilder</c>'s sheet table: a visual def carries no rig name, and
        /// deriving one from its id would be a guess that already breaks twice (the console skiff's
        /// visual is <c>ConsoleSkiff</c>, not <c>ConsoleIso</c>; the sport skiff's are
        /// <c>SportSkiffSingle/Twin</c>). Two paint builds of one hull share one deck, which is correct —
        /// paint does not move the sole.
        ///
        /// <para><c>FishingBoat</c> is absent on purpose: she is the hand-drawn compass, has no rig and
        /// therefore no measured deck, and keeps the deck-walk's greybox rectangle.</para>
        /// </summary>
        static readonly IReadOnlyDictionary<string, string[]> VisualsBySidecar = BuildWiring();

        static Dictionary<string, string[]> BuildWiring()
        {
            var wiring = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["doryIsoRig"]            = new[] { "DoryIso" },
                ["puntIsoRig"]            = new[] { "PuntIsoBasic", "PuntIsoUpgraded" },
                ["consoleIsoRig"]         = new[] { "ConsoleSkiff" },
                ["sportSkiffIsoRig"]      = new[] { "SportSkiffSingle", "SportSkiffTwin" },
                // The RESHAPED 7 m skiff — a second hull, not a rename of the row above. Her sidecar
                // is named for the HULL (no `Rig` suffix) because that is #541's convention and
                // because `sportSkiffMk2IsoRig` would collide conceptually with the v1's two visuals.
                ["sportSkiffMk2Iso"]      = new[] { "SportSkiffMk2Iso" },
                ["lobsterBoatIsoRig"]     = new[] { "LobsterBoatIso" },
                ["capeIslanderIsoRig"]    = new[] { "CapeIslanderIso" },
                ["sideDraggerIsoRig"]     = new[] { "SideDraggerIso" },
                ["sternTrawlerIsoRig"]    = new[] { "SternTrawlerIso" },
                ["sternTrawlerMk2IsoRig"] = new[] { "SternTrawlerMk2Iso" },
                ["coastalPacketIsoRig"]   = new[] { "CoastalPacketIso" },
                ["tankerIsoRig"]          = new[] { "TankerIso" },
            };

            // The lobster generator's eighteen wear one visual each, and every one of the three
            // names involved (sidecar stem, visual asset, deck def) is the SAME string with a
            // different case or suffix. Restating eighteen rows by hand would be eighteen chances to
            // wire a hull to her neighbour's deck — a defect that draws a plausible boat and is
            // invisible until somebody walks off the edge of one.
            foreach (LobsterVariant v in LobsterVariantFleet.All)
                wiring[v.SidecarStem] = new[] { v.AssetName };

            // The same argument, for the two families ADR 0022 phase 8's second half adds. The
            // sport skiff Mk2 is deliberately NOT here: she is one hull per rig and her sidecar stem
            // is `sportSkiffMk2Iso`, which the loop below cannot produce and the table above names
            // directly — right next to `sportSkiffIsoRig`, so a reader sees the two hulls are
            // separate wirings rather than one hull renamed.
            foreach (ZodiacBuild b in ZodiacFleet.All)
                wiring[b.SidecarStem] = new[] { b.AssetName };
            foreach (SportFisherHull h in SportFisherFleet.All)
                wiring[h.SidecarStem] = new[] { h.AssetName };

            return wiring;
        }

        [MenuItem("Hidden Harbours/Dev/Import Deck Sidecars (walkable DECK/WASHBOARD/CLEATS → Defs)",
                  priority = 122)]
        public static void ImportAll()
        {
            string report = Run(out int imported, out int refused);
            Debug.Log(report);
            if (refused > 0)
                Debug.LogError($"[DeckSidecarImporter] {refused} sidecar(s) REFUSED — see the report above. " +
                               "Those hulls keep the deck-walk's greybox rectangle until they are re-derived.");
            EditorUtility.DisplayDialog("Import deck sidecars",
                $"{imported} imported, {refused} refused.\n\nFull report in the Console.", "OK");
        }

        /// <summary>
        /// Headless entry (-executeMethod). Exits non-zero on any refusal, because a refused sidecar
        /// is not a partial success — that hull keeps the greybox rectangle and the next thing to
        /// notice is a player walking through her wheelhouse.
        /// </summary>
        public static void ImportAllCli()
        {
            try
            {
                Debug.Log(Run(out int imported, out int refused));
                if (refused > 0)
                {
                    Debug.LogError($"[DeckSidecarImporter] CLI import FAILED: {refused} refused, " +
                                   $"{imported} imported.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log($"[DeckSidecarImporter] CLI import OK — {imported} sidecar(s).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeckSidecarImporter] CLI import FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Do the import and hand back the report. Split out so a test (or a CI step) can run
        /// it without the dialog.</summary>
        public static string Run(out int imported, out int refused)
        {
            imported = 0;
            refused = 0;
            var log = new StringBuilder("[DeckSidecarImporter] rig sidecars → BoatDeckDef\n");

            string root = RepoRoot();
            string sidecarDir = Path.Combine(root, SidecarFolder);
            if (!Directory.Exists(sidecarDir))
            {
                log.Append($"  sidecar folder not found: {sidecarDir}");
                return log.ToString();
            }

            EnsureFolder(DeckFolder);

            string[] files = Directory.GetFiles(sidecarDir, "*.gameplay.json").OrderBy(f => f).ToArray();
            foreach (string file in files)
            {
                // The file's STEM names the hull (and therefore the Def); the rig it was cut from is
                // read out of the file. For the eleven single-hull sidecars the two are the same
                // string, so nothing about them changes — see DeckSidecarReader.ResolveRigFileName.
                string stem = Path.GetFileName(file).Replace(".gameplay.json", "");
                string json = File.ReadAllText(file);
                string rigFile = DeckSidecarReader.ResolveRigFileName(Path.GetFileName(file), json);
                string rigPath = Path.Combine(root, RigFolder, rigFile);
                byte[] rigBytes = File.Exists(rigPath) ? File.ReadAllBytes(rigPath) : null;

                if (rigBytes == null)
                {
                    refused++;
                    log.Append($"  ✗ {stem}: the rig it names ({rigFile}) is not in {RigFolder}.\n");
                    continue;
                }

                SidecarRead read = DeckSidecarReader.Read(json,
                                                          $"{SidecarFolder}/{Path.GetFileName(file)}", rigBytes);
                if (!read.Ok)
                {
                    refused++;
                    log.Append($"  ✗ {stem}: {string.Join(" | ", read.Errors)}\n");
                    continue;
                }

                BoatDeckDef def = WriteDeckAsset(stem, read, out string assetPath);
                int wired = WireVisuals(stem, def);
                imported++;

                log.Append($"  ✓ {stem}: {read.Areas.Count(a => !a.IsWashboard)} deck area(s), " +
                           $"{read.Areas.Count(a => a.IsWashboard)} washboard(s), {read.Cleats.Count} cleat(s), " +
                           $"walk box {Fmt(def.WalkHalfExtents * 2f)} m → {Path.GetFileName(assetPath)}, " +
                           $"{wired} visual(s) wired [{read.HashMatch}]\n");

                foreach (string note in read.Notes) log.Append($"      · {note}\n");

                float worst = def.Areas.Length == 0 ? 0f : def.Areas.Max(a => a.HeightResidualMax);
                if (worst > 0.05f)
                    log.Append($"      · height-plane fit is {worst:0.000} m off at its worst — that deck is " +
                               "not a plane and may want a real height field.\n");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.Append($"  — {imported} imported, {refused} refused.");
            return log.ToString();
        }

        // ---- writing -------------------------------------------------------------------------------

        static BoatDeckDef WriteDeckAsset(string sidecarStem, SidecarRead read, out string assetPath)
        {
            // lobsterBoatIsoRig → lobsterBoatIso; lobsterStandardHardtopFundyIso → itself (a
            // hull-named sidecar has no rig suffix to strip).
            string stem = StripRigSuffix(sidecarStem);
            assetPath = $"{DeckFolder}/{Capitalise(stem)}.asset";

            var def = AssetDatabase.LoadAssetAtPath<BoatDeckDef>(assetPath);
            bool fresh = def == null;
            if (fresh) def = ScriptableObject.CreateInstance<BoatDeckDef>();

            def.Id = "deck." + SnakeCase(stem);
            def.SourceSidecar = read.SidecarPath;
            def.SourceRig = $"{RigFolder}/{read.RigFileName}";
            def.DerivedFromRigSha256 = read.ExpectedRigSha;
            def.LoaMeters = read.LoaMeters;
            def.Areas = read.Areas.Select(BakeArea).ToArray();
            def.Cleats = read.Cleats
                .Select(c => new DeckCleat { Id = c.Id, Type = c.Type, PositionMeters = c.Position })
                .ToArray();

            BakeWalkBounds(def);

            if (fresh) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>One sidecar area as the runtime form. The bake itself lives on
        /// <see cref="DeckArea.From"/> so there is exactly one of it.</summary>
        static DeckArea BakeArea(SidecarArea src)
            => DeckArea.From(src.Id, src.IsWashboard ? DeckAreaKind.Washboard : DeckAreaKind.Deck,
                             src.Vertices);

        /// <summary>The box around every DECK area — what <c>DeckStance</c> publishes so consumers that
        /// grade against a rectangle grade against THIS hull. Washboards are deliberately outside it:
        /// they are not part of the free-walk area until the climb verb exists.</summary>
        static void BakeWalkBounds(BoatDeckDef def)
        {
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            bool any = false;

            foreach (DeckArea a in def.Areas)
            {
                if (a.Kind != DeckAreaKind.Deck || !a.IsUsable()) continue;
                any = true;
                minX = Mathf.Min(minX, a.Bounds.x);
                minY = Mathf.Min(minY, a.Bounds.y);
                maxX = Mathf.Max(maxX, a.Bounds.z);
                maxY = Mathf.Max(maxY, a.Bounds.w);
            }

            if (!any)
            {
                def.WalkCenter = Vector2.zero;
                def.WalkHalfExtents = Vector2.zero;
                return;
            }
            def.WalkCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            def.WalkHalfExtents = new Vector2((maxX - minX) * 0.5f, (maxY - minY) * 0.5f);
        }

        static int WireVisuals(string sidecarStem, BoatDeckDef def)
        {
            if (!VisualsBySidecar.TryGetValue(sidecarStem, out string[] names))
            {
                Debug.LogWarning($"[DeckSidecarImporter] '{sidecarStem}' has a sidecar but no entry in " +
                                 "VisualsBySidecar, so no hull wears its deck. Add the wiring row (or " +
                                 "say out loud that this hull has no visual yet).");
                return 0;
            }

            int wired = 0;
            foreach (string name in names)
            {
                string path = $"{VisualsFolder}/{name}.asset";
                var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
                if (visual == null)
                {
                    Debug.LogWarning($"[DeckSidecarImporter] '{sidecarStem}' wants to wire {path}, which " +
                                     "does not exist.");
                    continue;
                }
                if (visual.Deck == def) { wired++; continue; }   // already right — no needless re-dirty
                visual.Deck = def;
                EditorUtility.SetDirty(visual);
                wired++;
            }
            return wired;
        }

        // ---- naming / paths -------------------------------------------------------------------------

        /// <summary>The repo root — Unity's project folder is <c>&lt;repo&gt;/</c>, with Assets inside it.</summary>
        static string RepoRoot() => Directory.GetParent(Application.dataPath).FullName;

        static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>"lobsterBoatIsoRig" → "lobsterBoatIso". The trailing "Rig" is the rig FILE's suffix,
        /// not part of the hull's name.</summary>
        public static string StripRigSuffix(string rigBase)
            => rigBase != null && rigBase.EndsWith("Rig", StringComparison.Ordinal)
                ? rigBase.Substring(0, rigBase.Length - 3)
                : rigBase ?? "";

        /// <summary>"lobsterBoatIso" → "lobster_boat_iso" — the project's def-id convention
        /// (CLAUDE.md §5: <c>type.snake_case</c>).</summary>
        public static string SnakeCase(string camel)
        {
            if (string.IsNullOrEmpty(camel)) return "";
            var sb = new StringBuilder(camel.Length + 6);
            for (int i = 0; i < camel.Length; i++)
            {
                char c = camel[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static string Capitalise(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        static string Fmt(Vector2 v) => $"{v.x:0.00}×{v.y:0.00}";
    }
}
