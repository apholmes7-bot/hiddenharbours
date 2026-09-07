using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The authored index of every <see cref="InnerVoiceLineDef"/> the self-installing
    /// <see cref="InnerVoiceDirector"/> must find at boot — the <c>SeaweedLibrary</c> /
    /// <c>AmbientFleetLibrary</c> / <c>FishSpeciesLibrary</c> pattern exactly: the lines live one per
    /// file under <c>Data/NPCs/InnerVoice</c> (ADR 0003), this one asset gathers the references, and it
    /// lives in <c>Resources</c> so a <see cref="RuntimeInitializeOnLoadMethod"/> host can load it with
    /// no scene and no builder wiring.
    ///
    /// <para>Append-only: a new clue is a new def asset plus one entry here. Null entries are skipped, so
    /// a half-filled array costs the lines that are missing and nothing else.</para>
    ///
    /// <para>Create via Assets ▸ Create ▸ Hidden Harbours ▸ Inner Voice Library, save at
    /// <c>Resources/InnerVoiceLibrary</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Inner Voice Library", fileName = "InnerVoiceLibrary")]
    public sealed class InnerVoiceLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the director loads the library from at boot.</summary>
        public const string ResourcesPath = "InnerVoiceLibrary";

        [Tooltip("Every authored inner-voice line. One Def per file in Data/NPCs/InnerVoice; null " +
                 "entries are skipped.")]
        public InnerVoiceLineDef[] Lines = Array.Empty<InnerVoiceLineDef>();
    }
}
