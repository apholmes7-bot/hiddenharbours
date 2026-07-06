using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// An authored index of every <see cref="FishSpeciesDef"/> the runtime must resolve <b>by id</b> — the
    /// trap-fishing arc's answer to "a <see cref="TrapDef"/> names its catch species by id, how does the
    /// runtime find the Def?" (ADR 0003: content is data). One asset gathers the species refs; the
    /// self-installing <see cref="FishSpeciesRegistrar"/> loads it from <c>Resources</c> at boot and
    /// publishes each into <see cref="FishSpeciesRegistry"/>, so any system can resolve a fish id → Def with
    /// no scene/builder wiring — the exact <see cref="Core.IconLibrary"/> pattern.
    ///
    /// <para><b>Author it once, keep it append-only.</b> Add a species that a trap (or any future id-only
    /// consumer) must resolve by dropping its <see cref="FishSpeciesDef"/> into <see cref="Species"/>. It
    /// only needs to list the ids that get resolved by string at runtime (today: the trap catch species —
    /// lobster, crab); rod fishing still gets its pool as scene serialized refs and doesn't need the index.
    /// A future content-validation pass can assert every <see cref="TrapDef.AllowedCatchFishIds"/> entry
    /// appears here, the same way it checks the refs resolve.</para>
    ///
    /// <para>Create via Assets ▸ Create ▸ Hidden Harbours ▸ Fish Species Library, save at
    /// <c>Resources/FishSpeciesLibrary</c>. Pure content metadata: never serialized into a save, no
    /// determinism concern.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fish Species Library", fileName = "FishSpeciesLibrary")]
    public sealed class FishSpeciesLibrary : ScriptableObject
    {
        [Tooltip("Every species the runtime resolves BY ID (the trap catch species today). Each is published " +
                 "into FishSpeciesRegistry at boot. Append-only; a null entry is skipped.")]
        public FishSpeciesDef[] Species = Array.Empty<FishSpeciesDef>();

        /// <summary>Publish every non-null species into <see cref="FishSpeciesRegistry"/>.</summary>
        public void RegisterAll()
        {
            if (Species == null) return;
            for (int i = 0; i < Species.Length; i++)
                FishSpeciesRegistry.Register(Species[i]);
        }
    }
}
