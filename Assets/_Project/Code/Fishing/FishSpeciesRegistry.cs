using System;
using System.Collections.Generic;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A tiny Fishing-owned lookup that turns a stable fish <b>id</b> (<c>fish.lobster</c>) into its
    /// <see cref="FishSpeciesDef"/> at <b>runtime</b>. It is the id→def twin of Core's
    /// <see cref="Core.IconRegistry"/> (id→icon), and exists for the same reason: a system may hold a
    /// species only as a string and needs the real Def.
    ///
    /// <para><b>Why this had to be built (the gap the trap runtime exposed).</b> Rod fishing gets its
    /// <see cref="FishSpeciesDef"/> pool as <b>serialized refs</b> authored into the scene by the region
    /// builder (see <see cref="FishingController"/>). A <see cref="TrapDef"/>, by contrast, names its catch
    /// species by <b>id</b> only (<see cref="TrapDef.AllowedCatchFishIds"/>) — content is data by id
    /// (rule 2), and a trap can't carry a hard serialized ref to every species without breaking that. So the
    /// trap needs to resolve ids → Defs at runtime, and before this there was no runtime path: the only
    /// id→def resolution in the tree was the editor builders' <c>AssetDatabase</c> route (editor-only) and
    /// scene serialized refs. This registry is the smallest honest fix — an authored
    /// <see cref="FishSpeciesLibrary"/> loaded from <c>Resources</c> and published here at boot by
    /// <see cref="FishSpeciesRegistrar"/>, exactly the <see cref="Core.IconLibrary"/>/<c>IconRegistrar</c>
    /// pattern — rather than hacking per-trap serialized def refs.</para>
    ///
    /// <para><b>No save impact, no determinism concern.</b> Pure content metadata (authored def refs), never
    /// serialized, not part of the sim. The catch <em>roll</em> is deterministic elsewhere
    /// (<see cref="PlacedTrapCatch"/>); this is just the id→def bridge that feeds it the pool.</para>
    /// </summary>
    public static class FishSpeciesRegistry
    {
        // Keyed by the stable fish id, case-insensitive so a caller resolves whether it holds the id as
        // authored or normalized. First registration for a key wins (an authoring dup degrades gracefully
        // rather than flipping which def an id resolves to at runtime).
        private static readonly Dictionary<string, FishSpeciesDef> _byId =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Register a species under its stable <see cref="FishSpeciesDef.Id"/>. Idempotent and
        /// forgiving: a null def, a def with a blank id, or a re-registration of an id already present is a
        /// no-op (first wins). Call once per species at boot (the registrar, off the authored library).</summary>
        public static void Register(FishSpeciesDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Id)) return;
            if (_byId.ContainsKey(def.Id)) return;   // first registration wins
            _byId[def.Id] = def;
        }

        /// <summary>The registered def for <paramref name="id"/>, or <c>null</c> if none — so a caller can
        /// skip an unresolved id (an empty pool yields no catch) rather than throwing. Never throws.</summary>
        public static FishSpeciesDef Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _byId.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>True if a def has been registered for <paramref name="id"/>.</summary>
        public static bool Has(string id)
            => !string.IsNullOrWhiteSpace(id) && _byId.ContainsKey(id);

        /// <summary>
        /// Resolve a set of ids to their live Defs, in order, skipping any id that doesn't resolve. This is
        /// exactly how the trap turns <see cref="TrapDef.AllowedCatchFishIds"/> into the catch pool the
        /// resolver reads. A null/empty input yields an empty list (no catch), never null.
        /// </summary>
        public static List<FishSpeciesDef> Resolve(IReadOnlyList<string> ids)
        {
            var pool = new List<FishSpeciesDef>();
            if (ids == null) return pool;
            for (int i = 0; i < ids.Count; i++)
            {
                var def = Get(ids[i]);
                if (def != null) pool.Add(def);
            }
            return pool;
        }

        /// <summary>Number of registered species (tests / diagnostics).</summary>
        public static int Count => _byId.Count;

        /// <summary>Clear all mappings (scene teardown / tests), mirroring <see cref="Core.IconRegistry.Reset"/>.</summary>
        public static void Reset() => _byId.Clear();
    }
}
