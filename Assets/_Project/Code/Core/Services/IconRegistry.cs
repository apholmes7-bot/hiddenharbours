using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// A tiny Core-owned lookup so any UI can turn a stable content <b>id</b> — a fish/clam species id
    /// (<c>fish.atlantic_cod</c>), a gear id (<c>gear.rod</c>), a licence id (<c>license.cod</c>), a boat
    /// id (<c>boat.punt</c>), or a HUD glyph key (<c>ui.coin</c>, <c>ui.hold</c>) — into its <b>icon
    /// sprite</b> WITHOUT referencing the module that owns the content def. It is the icon twin of
    /// <see cref="RegionDisplayNames"/>: a Core seam both sides talk through, neither side gaining a
    /// reference to the other.
    ///
    /// <para><b>Why this exists (the lane problem it solves).</b> The sell screen and the catch card read
    /// a <see cref="CatchItem"/> (Core) — which deliberately caches only id/name/value so Boats/Economy
    /// depend on Core alone — and the HUD/UI assembly references only Core. So the UI cannot reach a
    /// <c>FishSpeciesDef.Sprite</c> (Fishing) or a gear/boat offer sprite (Economy) directly. The owning
    /// lanes (or a Core <c>IconRegistrar</c> reading an authored <c>IconLibrary</c>) <b>register</b> their
    /// def sprites here by id at boot; the UI <b>resolves</b> them by id. Sprites stay authored on the
    /// data (ADR 0003) — this is just the id→sprite bridge that keeps the UI data-driven and in-lane.</para>
    ///
    /// <para><b>No save impact, no determinism concern.</b> Pure presentation metadata (authored sprite
    /// refs), never serialized, not part of the sim — exactly like <see cref="RegionDisplayNames"/>.</para>
    /// </summary>
    public static class IconRegistry
    {
        // Keyed by the content id, ordinal/case-insensitive so a caller resolves whether it holds the id
        // as authored or normalized. First non-null registration for a key wins (authoring mistakes
        // degrade gracefully rather than flipping the icon at runtime).
        private static readonly Dictionary<string, Sprite> _byId =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Register an icon for a content id. Idempotent and forgiving: a blank id or a null sprite is
        /// ignored; the first non-null registration for an id wins. Call once per content item at boot
        /// (an icon registrar, or a def-owning module). Re-registering the same id is a no-op.
        /// </summary>
        public static void Register(string id, Sprite icon)
        {
            if (string.IsNullOrWhiteSpace(id) || icon == null) return;
            if (_byId.ContainsKey(id)) return; // first registration wins
            _byId[id] = icon;
        }

        /// <summary>True if an icon has been registered for <paramref name="id"/>.</summary>
        public static bool Has(string id)
            => !string.IsNullOrWhiteSpace(id) && _byId.ContainsKey(id);

        /// <summary>
        /// The registered icon for <paramref name="id"/>, or <c>null</c> if none — so a caller can fall
        /// back to a text-only presentation when an icon hasn't been registered (or the registry is
        /// empty in EditMode). Pure lookup; never throws.
        /// </summary>
        public static Sprite Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _byId.TryGetValue(id, out var icon) ? icon : null;
        }

        /// <summary>Number of registered icons (tests / diagnostics).</summary>
        public static int Count => _byId.Count;

        /// <summary>Clear all mappings (scene teardown / tests), mirroring <see cref="GameServices.Reset"/>.</summary>
        public static void Reset() => _byId.Clear();
    }
}
