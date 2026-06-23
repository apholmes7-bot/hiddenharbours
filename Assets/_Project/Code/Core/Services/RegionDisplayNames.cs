using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny Core-owned lookup so any module can turn a region's <b>scene name</b> (or stable region
    /// <b>id</b>) into the proper player-facing <b>display name</b> — "Coddle Cove", "Port Greywick" —
    /// without referencing the World module. It exists to resolve the crossing fade-card follow-up
    /// (ui-ux #54): the fade overlay reads only <c>SceneManager.activeSceneChanged</c> (a scene name)
    /// and must render "Port Greywick", not "Greywick" or "Greybox".
    ///
    /// <para><b>Who fills it, who reads it.</b> The <em>world</em> owns region data (<c>RegionDef</c>)
    /// and <b>registers</b> the scene-name/id → display-name mappings here at boot (World → Core is an
    /// allowed dependency). The <em>UI</em> (which references Core only) <b>reads</b> it via
    /// <see cref="Resolve"/>. Neither side gains a reference to the other — the seam stays in Core,
    /// exactly like <see cref="GameServices"/> and the EventBus.</para>
    ///
    /// <para><b>No save impact, no determinism concern.</b> This is pure presentation metadata
    /// (authored display strings), never serialized and not part of the sim.</para>
    /// </summary>
    public static class RegionDisplayNames
    {
        // Keyed by the lookup key (scene name OR region id), ordinal/case-insensitive so a scene named
        // "Greywick" resolves whether the registrar used the scene name or the id. First registration
        // for a key wins (authoring mistakes degrade gracefully rather than flipping at runtime).
        private static readonly Dictionary<string, string> _byKey =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Register a display name for a key (a scene name and/or a stable region id). Idempotent and
        /// forgiving: blank key or blank display name is ignored; the first non-blank registration for a
        /// key wins. Call once per region at boot (the world registrar) — re-registering the same
        /// key/value is a no-op.
        /// </summary>
        public static void Register(string key, string displayName)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(displayName)) return;
            if (_byKey.ContainsKey(key)) return; // first registration wins
            _byKey[key] = displayName;
        }

        /// <summary>
        /// True if a display name has been registered for <paramref name="key"/> (scene name or region id).
        /// </summary>
        public static bool Has(string key)
            => !string.IsNullOrWhiteSpace(key) && _byKey.ContainsKey(key);

        /// <summary>
        /// The registered display name for <paramref name="key"/>, or <c>null</c> if none — so a caller
        /// can fall back to its own derivation (e.g. the UI's camelCase-splitter) when a region hasn't
        /// registered. Pure lookup; never throws.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _byKey.TryGetValue(key, out var name) ? name : null;
        }

        /// <summary>
        /// The registered display name for <paramref name="key"/>, or <paramref name="fallback"/> when
        /// none is registered (the UI passes its own derived title as the fallback, so the card always
        /// shows <em>something</em> readable even for a region that hasn't registered yet).
        /// </summary>
        public static string Resolve(string key, string fallback)
            => Get(key) ?? fallback;

        /// <summary>Clear all mappings (scene teardown / tests), mirroring <see cref="GameServices.Reset"/>.</summary>
        public static void Reset() => _byKey.Clear();
    }
}
