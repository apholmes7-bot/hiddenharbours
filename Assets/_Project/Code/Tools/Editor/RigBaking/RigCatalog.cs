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
            // makes "his file is what runs" the whole point, and these rigs need nothing anyway
            // (no DOM, no ImageData, no console — measured in the spike).
            return File.ReadAllText(full);
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

            return new RigGeometry(
                width:      (int)host.EvaluateNumber($"{g}.W"),
                height:     (int)host.EvaluateNumber($"{g}.H"),
                pivotX:     host.EvaluateNumber($"{g}.pivot.x"),
                pivotY:     host.EvaluateNumber($"{g}.pivot.y"),
                nativeDirs: (int)host.EvaluateNumber($"{g}.DIRS"),
                rockFrames: (int)host.EvaluateNumber($"{g}.ROCK.frames"),
                defaultElevation: host.EvaluateNumber($"{g}.defaultElev"));
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
