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

        /// <summary>
        /// Catalog keys this rig DELEGATES TO — installed into the same host, transitively and in
        /// order, before this rig's own source runs. Empty for every rig that stands alone, which
        /// until the pass-6 character kit was all of them.
        ///
        /// <para>⚠️ <b>A missing prerequisite does not throw — it renders the wrong art.</b> The
        /// pass-6 body asks <c>root.HeadIso</c> for the hat table and falls back to its own local one
        /// when the head rig is absent; the face never stamps. That is the rigs' shared failure mode
        /// (resolve as <c>opts[k] ?? fallback</c>, never complain), which is why the dependency is
        /// declared HERE and not left to each caller to remember. Same principle as the canvas shim
        /// <c>CatchStorageBaker</c> installs: whatever a rig needs and does not provide is the HOST's
        /// job, never a patch to the art director's file (ADR 0021 §5).</para>
        /// </summary>
        public readonly IReadOnlyList<string> Prerequisites;

        public RigEntry(string scriptPath, string globalName, AzimuthConvention declared,
                        string[] prerequisites = null)
        {
            ScriptPath = scriptPath;
            GlobalName = globalName;
            DeclaredConvention = declared;
            Prerequisites = prerequisites ?? Array.Empty<string>();
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

                // ---- the character, pass 6 (drop of 2026-08-02, PR #397) — THREE FILES -----------
                //
                // The first rig in the catalog that is not one file. The body delegates skull / hair /
                // beard / hats to the head rig, which delegates the eye socket to the eye rig, so all
                // three must be in the host and IN THAT ORDER. Declared as prerequisites rather than
                // remembered by each caller: load them wrong and nothing throws — the body silently
                // uses its local HATS_LOCAL table and never stamps a face.
                //
                // The eye and head rigs expose no standard W/H/pivot triple, so they install with
                // InstallModule (the shellfish/catchKit path) and only the body reports geometry.
                ["characterEye"] = new RigEntry($"{RigFolder}/eyeIsoRig.js", "EyeIso",
                                                AzimuthConvention.Clockwise),

                ["characterHead"] = new RigEntry($"{RigFolder}/headIsoRig3.js", "HeadIso3",
                                                 AzimuthConvention.Clockwise,
                                                 prerequisites: new[] { "characterEye" }),

                // Still the non-boat host it always was: no ROCK block (characters RIDE a deck's rock
                // via opts.roll/pitch/heave rather than owning one), so Install reports rockFrames 0
                // and the turntable path does not apply. Baked by CharacterRigBaker (8 direction rows
                // × ANIMS-declared frames), never by the boat turntable.
                //
                // ⚠️ CLOCKWISE here is a PRIOR AGAIN, not the inherited pass-1 measurement. Pass 1 was
                // pixel-verified clockwise; pass 6 is a different renderer with a new head rig in the
                // projection path, and this lane has been CCW-mislabelled twice. CharacterRigAzimuthProbe
                // measures it from rendered pixels at bake time and the bake refuses on a mismatch.
                ["character"] = new RigEntry($"{RigFolder}/characterIsoRig6.js", "CharacterIso6",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "characterHead" }),

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

                // ---- the buildings (Nine Mile Creek + the St Peters village) --------------------

                // The clapboard houses. Same turntable, same elev 40°, same 32 px = 1 m as the fleet,
                // so a cottage and a Cape Islander stand in one space. In the README's INFERRED
                // counter-clockwise group and never measured — BuildingRigAzimuthProbe measures it at
                // bake time from the door anchor (a building has no bow taper for RigAzimuthProbe to
                // read) and the bake REFUSES on a mismatch, same as every sibling.
                //
                // ⚠️ Baked by BuildingRigBaker, never the boat turntable: the cell is 992×1060, so
                // eight facings in a row would be 7936 px — past the 4096 cap. The building baker
                // tight-crops to the drawn pixels first, which is what makes the bake possible at all.
                ["house"] = new RigEntry($"{RigFolder}/houseIsoRig.js", "HouseIso",
                                         AzimuthConvention.CounterClockwise),

                // The net-shed / storage-barn / fish-plant family — the wharf's working buildings.
                // Same story as the house, one size worse: the 1200×1160 cell is sized to hold the
                // `cannery`, so a net shed occupies a fraction of it and eight uncropped facings would
                // be 9600 px wide (the kit's own reference sheet, which is why that PNG stayed in
                // docs/ and was never imported).
                ["wharfBuilding"] = new RigEntry($"{RigFolder}/wharfBuildingRig.js", "WharfBuilding",
                                                 AzimuthConvention.CounterClockwise),

                // ---- the insides of those buildings (ADR 0021 §4; the interior pilot) ------------

                // THE INTERIOR IS THE BUILDING SEEN FROM INSIDE. Same turntable, same elev 40°, same
                // 32 px = 1 m, and — verified by measurement, not by the header — the SAME Wd/Ln/wallH
                // formulas as houseIsoRig, so a "cottage" interior registers under a "cottage"
                // exterior. Open-dollhouse cutaway: the camera-facing walls are dropped IN THE BAKE,
                // so visibility is art, not a runtime mask.
                //
                // ⚠️ Declared CCW like its exterior — but do NOT reach for BuildingRigAzimuthProbe to
                // check it. That probe reads which SIDE the door lands on at a quarter turn, and it
                // assumes the door is on the +Y gable because both exterior rigs put it there. THIS
                // RIG PUTS ITS DOOR ON −Y (anchors → pj(0,−Ln/2,fZ); the hearth takes +Y). Fed to that
                // probe the interior measures CLOCKWISE — a wrong answer with a confident report, and
                // the bake would then apply the opposite correction and mislabel every cell.
                // InteriorRigAzimuthProbe measures the door's gable FIRST and is what this entry is
                // cross-checked against.
                //
                // Cell 1180×900 and eight facings: past the 2048 the village kit imports at, so the
                // interior kit LIFTS its import cap and asserts native resolution instead
                // (InteriorKit.ImportSizeCap).
                ["interior"] = new RigEntry($"{RigFolder}/interiorIsoRig.js", "InteriorIso",
                                            AzimuthConvention.CounterClockwise),

                // The furniture that stands on that floor. Prop origin is the floor-centre of the
                // prop's footprint — the same pivot convention as the room — so a prop drops onto a
                // room floor point with no offset maths. Exposes project() like the room does, which is
                // how InteriorRigAzimuthProbe proves the two share one camera and one turntable rather
                // than declaring it. Its render() takes (name, dir, opts), not (dir, opts).
                ["interiorProp"] = new RigEntry($"{RigFolder}/interiorPropRig.js", "PropIso",
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
            InstallPrerequisites(host, entry);
            host.Execute(ReadSource(entry));
            string g = entry.GlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{entry.ScriptPath}' ran but did not install globalThis.{g}. " +
                    "Either the global name in the catalog is wrong or the rig changed shape.");
        }

        /// <summary>
        /// Runs a rig's declared <see cref="RigEntry.Prerequisites"/> — depth first, in order, each
        /// through <see cref="InstallModule"/> so its own prerequisites come first and its global is
        /// asserted. Already-present globals are skipped, so installing the body twice into one host
        /// (probe then bake) does not re-run the head.
        ///
        /// <para>A prerequisite is loaded with InstallModule and never Install: the head and eye rigs
        /// expose no <c>W/H/pivot</c> triple, and Install would throw on the missing pivot. Papering
        /// that over with defaults is exactly the silent-wrong-geometry failure the split entry points
        /// exist to prevent.</para>
        /// </summary>
        static void InstallPrerequisites(IRigScriptHost host, in RigEntry entry)
        {
            var prereqs = entry.Prerequisites;
            if (prereqs == null || prereqs.Count == 0) return;

            foreach (string key in prereqs)
            {
                var dep = Get(key);
                // Idempotent: a host that already carries the global has already run the file.
                if (host.EvaluateBool($"typeof {dep.GlobalName} === 'object' && " +
                                      $"{dep.GlobalName} !== null")) continue;
                InstallModule(host, dep);
            }
        }

        /// <summary>Loads a rig into a fresh host and returns its self-reported geometry.</summary>
        public static RigGeometry Install(IRigScriptHost host, in RigEntry entry)
        {
            InstallPrerequisites(host, entry);
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

        /// <summary>
        /// The Unity sprite pivot: normalised, BOTTOM-origin.
        ///
        /// <para>⚠️ <b>The y term is <c>(H − pivotY)/H</c>, NOT <c>(H − 1 − pivotY)/H</c>, and that
        /// is correct — it has been challenged and MEASURED.</b> See <b>ADR 0026</b> and
        /// <see cref="RigPivotConventionProbe"/>. In short: a rig's <c>pivot</c> is a CONTINUOUS
        /// coordinate whose origin is the cell's top-left corner, not a pixel index. The rigs
        /// project with <c>sy = cy − (…)·S</c> into a space the rasterizer samples at pixel
        /// CENTRES (<c>y + 0.5</c>), and every rig in the repo sets <c>cx = W/2</c> exactly — an
        /// integer only the continuous reading can produce, since a column index would need the
        /// half-integer <c>(W − 1)/2</c>. So <c>pivotY</c> lands on the pivot row's TOP edge and
        /// this formula is exact.</para>
        ///
        /// <para><b>The tree bake deliberately differs</b> (<c>TreeKitCatalog.NormalizedPivot</c>
        /// uses the rig's own <c>pad/cellH</c>, one row lower). That is not a contradiction — a
        /// tree's pivot is a chosen ROW, a hull's is a projected POINT. Do not unify them; ADR 0026
        /// has the argument and a test guards both directions.</para>
        /// </summary>
        public Vector2 UnityNormalisedPivot =>
            new Vector2((float)(PivotX / Width), (float)((Height - PivotY) / Height));

        public override string ToString() =>
            $"{Width}×{Height} px, pivot ({PivotX},{PivotY}) top-left, " +
            $"{NativeDirs} native dirs, {RockFrames} rock frames, elev {DefaultElevation}°";
    }
}
