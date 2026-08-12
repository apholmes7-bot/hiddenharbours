using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

    public static partial class RigCatalog
    {
        const string RigFolder = "docs/art/rigs";

        /// <summary>
        /// Marks a per-kit registration method on one of the <c>RigCatalog.&lt;Kit&gt;.cs</c> partials.
        /// <see cref="Assemble"/> finds every method carrying it and folds that kit's entries into
        /// <see cref="Entries"/>.
        ///
        /// <para>⚠️ THE ATTRIBUTE IS THE ONLY WIRING, and that is deliberate: there is no roster to
        /// append to, which is what stops two kit PRs in flight from colliding on a shared tail (three
        /// did, in one day, on 2026-08-12). The cost is that a kit file which forgets the attribute
        /// compiles cleanly and registers NOTHING — silently. <c>RigCatalogAssemblyTests</c> is what
        /// turns that into a red test rather than a missing sheet at bake time.</para>
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        sealed class RigContributionAttribute : Attribute
        {
        }

        /// <summary>
        /// The sink a kit file fills with <c>["key"] = new RigEntry(…)</c> — deliberately the same
        /// shape as the dictionary initializer this catalog used to be, so an entry moves between
        /// files without being re-typed and its measured comments travel with it unedited.
        ///
        /// <para>⚠️ The indexer APPENDS; it does not assign. A <c>Dictionary</c> initializer handed the
        /// same key twice keeps the last one and says nothing — precisely the failure a per-file split
        /// could otherwise introduce (two kit files, one key, no error, wrong art). Collecting the
        /// pairs instead is what lets <see cref="Assemble"/> see the collision and throw.</para>
        /// </summary>
        sealed class RigRegistration : IEnumerable<KeyValuePair<string, RigEntry>>
        {
            readonly List<KeyValuePair<string, RigEntry>> _items =
                new List<KeyValuePair<string, RigEntry>>();

            public RigEntry this[string key]
            {
                set => _items.Add(new KeyValuePair<string, RigEntry>(key, value));
            }

            public IEnumerator<KeyValuePair<string, RigEntry>> GetEnumerator() => _items.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Only the rigs Phase 1 actually bakes. The other 37 files in docs/art/rigs/ are imported
        /// source, and importing source is not a licence to wire content (CLAUDE.md rule 8) — most
        /// of the un-baked hulls are M2/M3 fleet.
        ///
        /// <para>Assembled once, at static init, from every <see cref="RigContributionAttribute"/>
        /// method across the <c>RigCatalog.&lt;Kit&gt;.cs</c> partials — one kit (or one tightly
        /// coupled family) per file. Registering a new kit means adding a FILE, never editing this
        /// one.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, RigEntry> Entries = Assemble();

        /// <summary>
        /// Folds every kit file's contribution into one catalog.
        ///
        /// <para><b>REGISTRATION ORDER DOES NOT MATTER, and that is enforced rather than requested.</b>
        /// Reflection returns methods in no defined order, so the result is made independent of it two
        /// ways: a duplicate key THROWS instead of letting the last writer win, and the catalog is a
        /// <see cref="SortedDictionary{TKey,TValue}"/> so <see cref="Entries"/> — and the "Known:" list
        /// in <see cref="Get"/>'s exception — read identically on every run and every machine.</para>
        ///
        /// <para>⚠️ A contribution must not read <see cref="Entries"/>: it runs DURING that field's
        /// initialization and would see null. Kit files build <see cref="RigEntry"/> values out of
        /// literals and <see cref="RigFolder"/> (a const, inlined at compile time) and nothing else.
        /// Prerequisites are declared as KEYS, not entries, and are resolved later by
        /// <see cref="InstallPrerequisites"/> — which is why a kit may name a prerequisite that lives
        /// in another file without caring which file loaded first.</para>
        /// </summary>
        static IReadOnlyDictionary<string, RigEntry> Assemble()
        {
            var contributions = typeof(RigCatalog)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.DeclaredOnly)
                .Where(m => m.IsDefined(typeof(RigContributionAttribute), inherit: false))
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .ToArray();

            if (contributions.Length == 0)
                throw new InvalidOperationException(
                    "RigCatalog assembled zero contributions. Every rig is registered by a " +
                    "[RigContribution] method in a RigCatalog.<Kit>.cs partial — if this fired, those " +
                    "files did not compile into this assembly.");

            var catalog = new SortedDictionary<string, RigEntry>(StringComparer.Ordinal);
            var declaredBy = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var method in contributions)
            {
                if (!(method.Invoke(null, null) is IEnumerable<KeyValuePair<string, RigEntry>> kit))
                    throw new InvalidOperationException(
                        $"RigCatalog.{method.Name}() returned null, or a shape the assembler cannot " +
                        "read. A [RigContribution] method returns its kit's RigRegistration.");

                foreach (var pair in kit)
                {
                    if (declaredBy.TryGetValue(pair.Key, out string first))
                        throw new InvalidOperationException(
                            $"Two rig contributions both register the key '{pair.Key}': " +
                            $"RigCatalog.{first}() and RigCatalog.{method.Name}(). Rig ids are unique " +
                            "and append-only (CLAUDE.md §5), so one of the two kit files is wrong. " +
                            "Left to a dictionary initializer this would NOT have thrown — whichever " +
                            "ran last would silently win and one kit would bake the other's art.");

                    declaredBy[pair.Key] = method.Name;
                    catalog.Add(pair.Key, pair.Value);
                }
            }

            return catalog;
        }

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
