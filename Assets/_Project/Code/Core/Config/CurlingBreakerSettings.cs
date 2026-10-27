using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Presentation budgets and phase keys for the bounded curling sheet.</summary>
    [Serializable]
    public struct CurlingBreakerSettings
    {
        public const int PilotCapacity = 1;
        [Range(0, 1)] public int Master;
        [Range(0, 8)] public int MaxCurls;
        [Range(0f, 0.5f)] public float HeightFraction;
        [Min(0f)] public float EndTaperMeters;
        [Range(3, 17)] public int AlongColumns;
        [Range(5, 25)] public int CrossRows;
        public float SteepenStart;
        public float ThrowStart;
        public float Crest;
        public float Land;
        public float Gone;
        [Range(0f, 1f)] public float MinBore;
        [Range(0f, 1f)] public float MinWhitewater;
        [Range(0.5f, 1f)] public float MinPlungingWeight;
        [Range(0f, 1f)] public float MaskScale;
        [Range(0f, 32f)] public float MinReadableHeight;
        [Range(0f, 32f)] public float MinReadableThrow;
        [Range(0, 60)] public int ReadableFrameBar;
        [Min(1f)] public float DefaultPixelsPerMetre;
        [Min(1)] public int FrameRate;

        public bool Enabled => Master == 1 && MaxCurls == PilotCapacity && HeightFraction > 0f
                               && MaskScale > 0f;

        public bool IsValid => Master >= 0 && Master <= 1 && MaxCurls >= 0 && MaxCurls <= PilotCapacity
            && HeightFraction >= 0f && HeightFraction <= 0.5f && EndTaperMeters > 0f
            && AlongColumns >= 3 && AlongColumns <= 17 && AlongColumns % 2 == 1
            && CrossRows >= 5 && CrossRows <= 25 && SteepenStart >= -0.25f
            && SteepenStart <= ThrowStart && ThrowStart <= Crest && Crest == 0f
            && Crest <= Land && Land <= Gone && Gone <= 0.25f
            && MinBore >= 0f && MinBore <= 1f && MinWhitewater >= 0f && MinWhitewater <= 1f
            && MinPlungingWeight >= 0.5f && MinPlungingWeight <= 1f
            && (MaskScale == 0f || MaskScale >= 0.25f && MaskScale <= 1f)
            && MinReadableHeight >= 0f && MinReadableHeight <= 32f
            && MinReadableThrow >= 0f && MinReadableThrow <= 32f
            && ReadableFrameBar >= 0 && ReadableFrameBar <= 60
            && DefaultPixelsPerMetre > 0f && FrameRate > 0;

        public static CurlingBreakerSettings Default => new CurlingBreakerSettings
        {
            Master = 0, MaxCurls = 1, HeightFraction = 0.5f, EndTaperMeters = 1f,
            AlongColumns = 9, CrossRows = 13,
            SteepenStart = -0.12f, ThrowStart = -0.04f, Crest = 0f, Land = 0.08f, Gone = 0.12f,
            MinBore = 0.1f, MinWhitewater = 0.1f, MinPlungingWeight = 0.5f,
            MaskScale = 0.5f, MinReadableHeight = 8f, MinReadableThrow = 4f,
            ReadableFrameBar = 12, DefaultPixelsPerMetre = 96f, FrameRate = 60,
        };
    }
}
