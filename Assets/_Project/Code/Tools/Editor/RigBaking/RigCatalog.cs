using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// A rig the baker knows how to load: where the art director's file lives and what global it
    /// installs. Deliberately thin — cell size, pivot, facing count and rock frames are read FROM
    /// THE RIG at bake time (ADR 0021 §4: "cell geometry, pivot and the crop rect come from the rig
    /// instead of a README"), so there is no hand-maintained table to drift.
    /// </summary>
    public readonly struct RigEntry
    {
        /// <summary>Path relative to the repo root, e.g. "docs/art/rigs/puntIsoRig.js".</summary>
        public readonly string ScriptPath;

        /// <summary>The global the IIFE installs, e.g. "PuntIso".</summary>
        public readonly string GlobalName;

        /// <summary>
        /// What <c>docs/art/rigs/README.md</c> DECLARES this rig's convention to be.
        ///
        /// ⚠️ This is an EXPECTATION TO CROSS-CHECK, not an input to the bake. The baker uses the
        /// value <see cref="RigAzimuthProbe"/> measures from rendered pixels. If the two disagree
        /// the bake FAILS LOUDLY rather than silently picking one — because a silent pick is
        /// exactly how this mislabel shipped defects in five separate kits. If you are here because
        /// a rig now fails that cross-check, the fix is to correct the README, having first
        /// confirmed the measurement by eye.
        /// </summary>
        public readonly AzimuthConvention DeclaredConvention;

        public RigEntry(string scriptPath, string globalName, AzimuthConvention declared)
        {
            ScriptPath = scriptPath;
            GlobalName = globalName;
            DeclaredConvention = declared;
        }
    }

    public static class RigCatalog
    {
        const string RigFolder = "docs/art/rigs";

        /// <summary>
        /// Only the rigs Phase 1 actually bakes. The other 37 files in docs/art/rigs/ are imported
        /// source, and importing source is not a licence to wire content (CLAUDE.md rule 8) — most
        /// of the un-baked hulls are M2/M3 fleet.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, RigEntry> Entries =
            new Dictionary<string, RigEntry>(StringComparer.Ordinal)
            {
                // The golden-master probe. Both its TUBS are at x:0 — see RigAzimuthProbe.
                ["punt"] = new RigEntry($"{RigFolder}/puntIsoRig.js", "PuntIso",
                                        AzimuthConvention.CounterClockwise),

                // The reason Phase 1 exists: Tier 3, ~12.0 m LOA, and no baked art anywhere.
                ["lobsterBoat"] = new RigEntry($"{RigFolder}/lobsterBoatIsoRig.js", "LobsterBoatIso",
                                               AzimuthConvention.CounterClockwise),

                // The first non-boat host: fixed CLOCKWISE at source by the art director
                // (th = −dir·45°), unlike every boat. It exposes no ROCK block — characters RIDE a
                // deck's rock via opts.roll/pitch/heave rather than owning one — so Install reports
                // rockFrames 0 and the turntable path does not apply. Baked by CharacterRigBaker
                // (8 direction rows × ANIMS-declared frames), never by the boat turntable.
                ["character"] = new RigEntry($"{RigFolder}/characterIsoRig.js", "CharacterIso",
                                             AzimuthConvention.Clockwise),

                // ---- the fishing kit (drop of 2026-07-22, PR #258) — Rod Fishing v2 wave 3 ------

                // Parametric fish loft: one skeleton, a SPECIES data table. CLAIMED clockwise
                // (th = −dir·45°, same term as the character) but the kit is UNVERIFIED — the
                // README's correction note is explicit that the sign term is not proof, so
                // FishingRigAzimuthProbe measures the head side from pixels before any bake.
                // Declares no DIRS global (Install reports 0); the 8 headings are the ADR-0006
                // recipe, supplied by FishingKitBaker.
                ["fish"] = new RigEntry($"{RigFolder}/fishIsoRig.js", "FishIso",
                                        AzimuthConvention.Clockwise),

                // One of the two rigs the art director fixed CLOCKWISE at source (README's
                // pixel-verified group). Still measured at bake time like everything else —
                // FishingRigAzimuthProbe reads which side the blank extends at the E/W rows.
                ["rod"] = new RigEntry($"{RigFolder}/rodIsoRig.js", "RodIso",
                                       AzimuthConvention.Clockwise),

                // NOT DIRECTIONAL: a 16×22 state sprite (float/nibble/strike/fly) with no azimuth
                // term at all — render(state, frame), no dir argument. The declared convention
                // below is a placeholder that nothing reads and nothing probes; the bobber bake
                // path never consults it and never calls DirForCell.
                ["bobber"] = new RigEntry($"{RigFolder}/bobberRig.js", "RodBobber",
                                          AzimuthConvention.Clockwise),

                // ---- the storage wave (catch-handling pass) — container fills ------------------

                // CONTINUOUS heading (ang in radians), not an 8-way turntable — fills scatter it.
                // The declared convention is a placeholder nothing probes; the storage bake renders
                // the exact per-variant angles CatchKit.item composes.
                ["crustacean"] = new RigEntry($"{RigFolder}/crustaceanRig.js", "Crustacean",
                                              AzimuthConvention.Clockwise),

                // NOT DIRECTIONAL: 14×12 item lays + 22×16 handfuls, no camera. Exposes IW/IH/
                // ipivot instead of W/H/pivot, so it must be loaded with InstallModule, never
                // Install. Placeholder convention, nothing probes it.
                ["shellfish"] = new RigEntry($"{RigFolder}/shellfishRig.js", "Shellfish",
                                             AzimuthConvention.Clockwise),

                // THE GLUE, not a renderer: item()/fillItems()/tintSpoil over the other catch
                // rigs. No cell geometry of its own (InstallModule only) and — uniquely — it calls
                // document.createElement('canvas') inside item(), so the storage baker installs a
                // host-side canvas shim FIRST (ADR 0021 §5: anything the engine needs that the rig
                // doesn't provide belongs in OUR host code, never in his file).
                ["catchKit"] = new RigEntry($"{RigFolder}/catchKit.js", "CatchKit",
                                            AzimuthConvention.Clockwise),

                // The insulated deck tote. README claims CLOCKWISE (th = −dir·45°, the character/
                // fish term, and the fish MEASURED clockwise in the #265 bake) — still measured
                // from pixels by StorageRigAzimuthProbe before any bake, per the correction note.
                ["fishTote"] = new RigEntry($"{RigFolder}/fishToteRig.js", "FishTote",
                                            AzimuthConvention.Clockwise),

                // Pails + fish tray. README's CCW-inferred group (th = +dir·45°, the boat term) —
                // measured from pixels (tray-footprint chirality) before any bake. Exposes
                // pivotCarry/pivotRest instead of pivot → InstallModule; the storage baker reads
                // the REST pivot itself (rest mode is the only mode it bakes).
                ["bucket"] = new RigEntry($"{RigFolder}/bucketRig.js", "BucketIso",
                                          AzimuthConvention.CounterClockwise),
            };

        public static string RepoRoot =>
            Directory.GetParent(Application.dataPath)!.FullName;

        public static RigEntry Get(string key) =>
            Entries.TryGetValue(key, out var e)
                ? e
                : throw new ArgumentException(
                    $"No rig '{key}' in the catalog. Known: {string.Join(", ", Entries.Keys)}.");

        public static string ReadSource(in RigEntry entry)
        {
            string full = Path.Combine(RepoRoot, entry.ScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Rig source missing at {full}. The rigs are committed under docs/art/rigs/ — " +
                    "if this fired, the branch predates that import.", full);

            // Read and hand over UNMODIFIED. No preamble, no shim, no patched globals: ADR 0021 §5
            // makes "his file is what runs" the whole point. Where a rig genuinely needs an
            // environment global the file doesn't provide (catchKit's document.createElement),
            // the HOST installs the shim as separate host-side code BEFORE this source runs
            // (CatchStorageBaker.CanvasShimJs) — the file itself is never touched.
            return File.ReadAllText(full);
        }

        /// <summary>
        /// Loads a rig that declares NO standard cell geometry (no <c>W/H/pivot</c> globals) —
        /// the kits and item rigs (shellfishRig's IW/ipivot, catchKit's functions, bucketRig's
        /// dual pivots). Executes the source and asserts the global installed, nothing more; the
        /// caller reads whatever shape the rig actually exposes. <see cref="Install"/> would throw
        /// on the missing <c>pivot</c>, and papering that over with defaults would silently bake
        /// a wrong pivot — hence a separate, geometry-free entry point.
        /// </summary>
        public static void InstallModule(IRigScriptHost host, in RigEntry entry)
        {
            host.Execute(ReadSource(entry));
            string g = entry.GlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{entry.ScriptPath}' ran but did not install globalThis.{g}. " +
                    "Either the global name in the catalog is wrong or the rig changed shape.");
        }

        /// <summary>Loads a rig into a fresh host and returns its self-reported geometry.</summary>
        public static RigGeometry Install(IRigScriptHost host, in RigEntry entry)
        {
            host.Execute(ReadSource(entry));
            string g = entry.GlobalName;

            // Assert the global really installed before trusting anything downstream.
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{entry.ScriptPath}' ran but did not install globalThis.{g}. " +
                    "Either the global name in the catalog is wrong or the rig changed shape.");

            // ROCK is a HULL contract: boats own their rock cycle and export its frame count.
            // Character rigs have no ROCK at all — they ride a deck's rock through
            // opts.roll/pitch/heave instead — so its absence is a legitimate rig shape, not an
            // error. Report 0 rather than throwing; the boat turntable never runs on such a rig
            // (RigBakeMenu's recipes name boat keys only) and CharacterRigBaker never reads it.
            bool hasRock = host.EvaluateBool($"typeof {g}.ROCK === 'object' && {g}.ROCK !== null");

            // DIRS is likewise a rig-shape fact, not a universal: the fishing kit's rigs
            // (FishIso, RodIso, RodBobber) declare no DIRS global. 0 = "the rig does not say" —
            // a directional baker then supplies its recipe's facing count (8 per ADR-0006) and a
            // non-directional one (the bobber) never asks. Same for defaultElev: the bobber is a
            // hand-plotted sprite with no camera at all.
            bool hasDirs = host.EvaluateBool($"typeof {g}.DIRS === 'number'");
            bool hasElev = host.EvaluateBool($"typeof {g}.defaultElev === 'number'");

            return new RigGeometry(
                width:      (int)host.EvaluateNumber($"{g}.W"),
                height:     (int)host.EvaluateNumber($"{g}.H"),
                pivotX:     host.EvaluateNumber($"{g}.pivot.x"),
                pivotY:     host.EvaluateNumber($"{g}.pivot.y"),
                nativeDirs: hasDirs ? (int)host.EvaluateNumber($"{g}.DIRS") : 0,
                rockFrames: hasRock ? (int)host.EvaluateNumber($"{g}.ROCK.frames") : 0,
                defaultElevation: hasElev ? host.EvaluateNumber($"{g}.defaultElev") : 0);
        }
    }

    /// <summary>Geometry read from the rig itself, never from a README.</summary>
    public readonly struct RigGeometry
    {
        public readonly int Width, Height, NativeDirs, RockFrames;
        /// <summary>Pivot in cell pixels, measured from the TOP-LEFT (the rigs' screen origin).
        /// Unity sprite pivots are normalised from the BOTTOM-LEFT, so converting is
        /// <c>(pivotX / W, (H - pivotY) / H)</c> — that is where PuntIso's 0.44047618 comes from
        /// (168 − 94) / 168, and getting it upside-down is an easy and silent mistake.</summary>
        public readonly double PivotX, PivotY;
        public readonly double DefaultElevation;

        public RigGeometry(int width, int height, double pivotX, double pivotY,
                           int nativeDirs, int rockFrames, double defaultElevation)
        {
            Width = width; Height = height; PivotX = pivotX; PivotY = pivotY;
            NativeDirs = nativeDirs; RockFrames = rockFrames; DefaultElevation = defaultElevation;
        }

        public Vector2 UnityNormalisedPivot =>
            new Vector2((float)(PivotX / Width), (float)((Height - PivotY) / Height));

        public override string ToString() =>
            $"{Width}×{Height} px, pivot ({PivotX},{PivotY}) top-left, " +
            $"{NativeDirs} native dirs, {RockFrames} rock frames, elev {DefaultElevation}°";
    }
}
