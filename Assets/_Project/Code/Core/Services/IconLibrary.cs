using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// An authored id → icon table (ADR 0003: content is data). One asset maps stable content ids —
    /// fish/clam species, gear, licences, boats, and HUD glyph keys — to their imported icon sprites,
    /// so the UI can show the right picture for an id it only knows as a string (see
    /// <see cref="IconRegistry"/> for why the UI can't reach the owning module's def directly).
    ///
    /// <para><b>Why a Core asset rather than the defs alone.</b> The fish def already carries its own
    /// <c>Sprite</c> (and that ref is assigned too), but the sell screen / catch card / HUD see only a
    /// Core id, never the Fishing/Economy def. Several icon-bearing ids (gear, licence, boat, the coin and
    /// hold glyphs) live on defs the UI lane doesn't own or that have no sprite field at all. This single
    /// Core asset gathers every UI-facing icon ref in one place the UI lane can author, then an
    /// <c>IconRegistrar</c> publishes it into <see cref="IconRegistry"/> at boot. It references only the
    /// imported sprite assets — no other code module — so it stays squarely in Core.</para>
    ///
    /// <para>Loaded from <c>Resources</c> by the self-installing icon registrar (no scene/builder wiring),
    /// mirroring how <see cref="SaveService"/> bootstraps itself. Pure presentation metadata: never
    /// serialized into a save, no determinism concern.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Icon Library", fileName = "IconLibrary")]
    public sealed class IconLibrary : ScriptableObject
    {
        /// <summary>One id → icon mapping. The id is a stable content id (e.g. <c>fish.atlantic_cod</c>,
        /// <c>gear.rod</c>, <c>license.cod</c>, <c>boat.punt</c>, <c>ui.coin</c>).</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("Stable content id this icon belongs to (fish/gear/licence/boat id, or a ui.* glyph key).")]
            public string Id;
            [Tooltip("The imported icon sprite for this id.")]
            public Sprite Icon;
        }

        [Tooltip("The id → icon mappings published into IconRegistry at boot.")]
        public Entry[] Entries = Array.Empty<Entry>();

        /// <summary>Publish every non-blank, non-null entry into <see cref="IconRegistry"/>.</summary>
        public void RegisterAll()
        {
            if (Entries == null) return;
            for (int i = 0; i < Entries.Length; i++)
                IconRegistry.Register(Entries[i].Id, Entries[i].Icon);
        }
    }
}
