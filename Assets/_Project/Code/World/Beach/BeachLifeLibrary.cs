using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Every beach activity the game ships, in one list a boot-time host can load from
    /// <c>Resources</c> (the <see cref="ConversationLibrary"/> idiom). The activities themselves stay one
    /// per file in <c>Data/Beaches</c>; this only names them.
    ///
    /// <para><b>The order is the priority.</b> A villager takes the FIRST activity whose gates pass, so
    /// the rare ones (the flats are only bare for an hour or two) go before the everyday ones.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Beach Life Library", fileName = "BeachLifeLibrary")]
    public sealed class BeachLifeLibrary : ScriptableObject
    {
        /// <summary>Where the shipped library sits under a <c>Resources</c> folder.</summary>
        public const string ResourcesPath = "BeachLifeLibrary";

        [Tooltip("Every authored beach activity, most particular first. Null entries are skipped.")]
        public BeachActivityDef[] Activities = Array.Empty<BeachActivityDef>();
    }
}
