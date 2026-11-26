using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>AN ISLANDER'S PHOTO REQUEST, AS DATA</b> (owner 2026-11-26: the seals on the Whelps are the first
    /// camera practice, and the cottage dweller asks for a picture of them). One asset per request under
    /// <c>Data/Resources/PhotoRequests</c>, keyed by a stable, append-only <see cref="Id"/>.
    ///
    /// <para><b>No state of its own, and none saved.</b> Where the request stands is read from flags the
    /// world already keeps (asked, pictured, done) and from the album (save v28): the
    /// <see cref="QuestDef"/> doctrine. Asking may hand him the camera: <see cref="AskedFlag"/> can be the
    /// camera's own owned flag.</para>
    ///
    /// <para><b>Hints, not instructions</b> (owner 2026-11-25: clues from citizens, never instructed up
    /// front). Every line is authored here; nothing here names a key.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Photo Request", fileName = "PhotoRequest")]
    public sealed class PhotoRequestDef : ScriptableObject
    {
        public const string IdPrefix = "photo_request.";

        /// <summary>Where the shipped requests are, for <c>Resources.LoadAll</c>.</summary>
        public const string ResourcesFolder = "PhotoRequests";

        [Header("Identity")]
        [Tooltip("Stable id, append-only (photo_request.snake_case).")]
        public string Id = IdPrefix;

        [Header("Who asks")]
        [Tooltip("The NpcDef id of the islander who asks and thanks (npc.snake_case).")]
        public string GiverNpcId = "";

        [Tooltip("Optional. A flag that must be set before she asks (empty: she asks the first time).")]
        public string RequiresFlag = "";

        [Header("What she wants")]
        [Tooltip("The subject id the picture must hold (wildlife.snake_case).")]
        public string SubjectId = "";

        [Tooltip("At least this many of them in the one picture.")]
        [Min(1)] public int MinCount = 1;

        [Tooltip("The best of them doing nothing worse than this (in WildlifePose order: Resting is best).")]
        public WildlifePose WorstPose = WildlifePose.Alert;

        [Tooltip("The picture's score for that subject must be at least this (0..100).")]
        [Range(0, 100)] public int MinScore = 30;

        [Header("Flags (all settable here; the quest reads them)")]
        [Tooltip("Set when she has asked. May be the camera's owned flag: she hands it over as she asks.")]
        public string AskedFlag = "";

        [Tooltip("Set once his album holds a picture that would do.")]
        public string PicturedFlag = "";

        [Tooltip("Set when she has thanked him.")]
        public string DoneFlag = "";

        [Header("Her thanks")]
        [Tooltip("Money she presses on him with her thanks. 0 = words only.")]
        [Min(0)] public int RewardMoney = 0;

        [Header("Words (after her own lines)")]
        [Tooltip("The asking: hints at where and when, never which key.")]
        public string[] AskLines = Array.Empty<string>();

        [Tooltip("Asked, no picture yet that would do.")]
        public string[] ReminderLines = Array.Empty<string>();

        [Tooltip("When she sees a picture that does.")]
        public string[] ThanksLines = Array.Empty<string>();

        [Tooltip("Optional. After she has thanked him.")]
        public string[] AfterLines = Array.Empty<string>();

        /// <summary>Null when the def is whole; otherwise the first thing wrong with it.</summary>
        public string ValidationError()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith(IdPrefix, StringComparison.Ordinal) ||
                Id.Length == IdPrefix.Length)
                return "Expected a photo_request.* id.";
            if (string.IsNullOrWhiteSpace(GiverNpcId) || !GiverNpcId.StartsWith("npc.", StringComparison.Ordinal))
                return "The giver must be an npc.* id.";
            if (string.IsNullOrWhiteSpace(SubjectId) || !SubjectId.StartsWith("wildlife.", StringComparison.Ordinal))
                return "The subject must be a wildlife.* id.";
            if (string.IsNullOrWhiteSpace(AskedFlag) || string.IsNullOrWhiteSpace(PicturedFlag) ||
                string.IsNullOrWhiteSpace(DoneFlag))
                return "Asked, pictured and done each need a flag.";
            if (AskedFlag == PicturedFlag || AskedFlag == DoneFlag || PicturedFlag == DoneFlag)
                return "Asked, pictured and done must be three different flags.";
            if (!HasLine(AskLines)) return "She has no asking lines.";
            if (!HasLine(ReminderLines)) return "She has no reminder lines.";
            if (!HasLine(ThanksLines)) return "She has no thanks lines.";
            return null;
        }

        private static bool HasLine(string[] lines)
        {
            if (lines == null) return false;
            for (int i = 0; i < lines.Length; i++) if (!string.IsNullOrWhiteSpace(lines[i])) return true;
            return false;
        }
    }
}
