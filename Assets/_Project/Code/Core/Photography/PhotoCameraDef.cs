using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One kind of animal the camera knows how to name in the album. Keyed by the species def id the
    /// wildlife seam reports (<see cref="WildlifeSighting.SubjectId"/>, <c>wildlife.grey_seal</c>).
    /// </summary>
    [Serializable]
    public sealed class PhotoSubjectEntry
    {
        [Tooltip("The subject id, exactly as the wildlife seam reports it (wildlife.snake_case).")]
        public string SubjectId = "";

        [Tooltip("What the album calls one of them (\"grey seal\").")]
        public string DisplayName = "";

        [Tooltip("What the album calls several (\"grey seals\").")]
        public string PluralName = "";
    }

    /// <summary>What the album calls one region.</summary>
    [Serializable]
    public sealed class PhotoPlaceEntry
    {
        [Tooltip("The region id (region.snake_case).")]
        public string RegionId = "";

        [Tooltip("The place as the album writes it (\"St Peters\").")]
        public string Name = "";
    }

    /// <summary>
    /// <b>THE PLAYER'S CAMERA, AS DATA</b> (BP-09, BP-11; owner 2026-11-13: "always on the player and uses
    /// P button"). One asset, <c>Data/Resources/Photography/PhotoCamera</c>: how big the viewfinder is,
    /// how far it reaches, how a picture is scored, how big the album is, what the album calls each
    /// subject and every word the camera says. No number or word of the camera is written in C#
    /// (rules 2 and 6).
    ///
    /// <para><b>In Core</b> because three modules read it and none may reference another: the camera
    /// itself (Player), the viewfinder (UI) and the album page and the islanders' photo requests
    /// (World).</para>
    ///
    /// <para><b>Owning it.</b> Once owned, the camera is always carried and never takes the hand slot
    /// (BP-09). Owning is a saved flag the giver sets (<see cref="OwnedWhileFlag"/>): no new save field.
    /// Empty = always owned.</para>
    /// FLAG lead-architect: new Core contract (the camera's def and its scoring numbers).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Photo Camera", fileName = "PhotoCamera")]
    public sealed class PhotoCameraDef : ScriptableObject
    {
        public const string IdPrefix = "photo_camera.";

        /// <summary>Where the shipped camera is, for <c>Resources.Load</c> (no extension).</summary>
        public const string ResourcesPath = "Photography/PhotoCamera";

        /// <summary>How many poses <see cref="WildlifePose"/> has; the per-pose tables are this long.</summary>
        public const int PoseCount = 5;

        [Header("Identity")]
        [Tooltip("Stable id, append-only (photo_camera.snake_case).")]
        public string Id = IdPrefix;

        [Tooltip("What the camera is called.")]
        public string DisplayName = "";

        [Header("Owning it")]
        [Tooltip("The saved flag that means he has the camera. Empty = always his.")]
        public string OwnedWhileFlag = "";

        [Header("The viewfinder (world metres)")]
        [Tooltip("Width of the patch of world the picture takes in.")]
        [Min(1f)] public float FrameWidthMetres = 10f;

        [Tooltip("Height of the patch of world the picture takes in.")]
        [Min(1f)] public float FrameHeightMetres = 7f;

        [Tooltip("How far ahead of him the frame stands when the camera comes up.")]
        [Min(0f)] public float StartReachMetres = 9f;

        [Tooltip("The farthest the frame can be pushed from him while aiming.")]
        [Min(0f)] public float MaxReachMetres = 26f;

        [Tooltip("How fast the move keys slide the frame while the camera is up.")]
        [Min(0f)] public float AimSpeedMetresPerSecond = 9f;

        [Tooltip("The least time between two pictures.")]
        [Min(0f)] public float ShutterCooldownSeconds = 0.75f;

        [Tooltip("How long the line about the last picture stays under the frame.")]
        [Min(0f)] public float ResultShowSeconds = 3f;

        [Tooltip("How long the white flash of the shutter lasts.")]
        [Min(0f)] public float FlashSeconds = 0.15f;

        [Tooltip("How many animals are looked at per haul-out when the shutter goes (the read buffer).")]
        [Range(1, 64)] public int MaxSightingsRead = 32;

        [Header("Scoring")]
        [Tooltip("Inside this distance from him an animal is as sharp as a picture gets.")]
        [Min(0f)] public float FullQualityDistanceMetres = 14f;

        [Tooltip("At and past this distance an animal is only FarQuality sharp.")]
        [Min(0f)] public float FarDistanceMetres = 32f;

        [Tooltip("How sharp an animal at FarDistanceMetres or beyond is (0..1).")]
        [Range(0f, 1f)] public float FarQuality = 0.3f;

        [Tooltip("How much an animal at the frame's edge loses against one in its middle (0..1).")]
        [Range(0f, 1f)] public float CentreWeight = 0.3f;

        [Tooltip("How good a picture each pose makes, in WildlifePose order: Resting, Alert, Sliding, " +
                 "Swimming, Diving (0..1).")]
        public float[] PoseQuality = { 1f, 0.75f, 0.45f, 0.55f, 0f };

        [Tooltip("Points added for every animal of a kind past the first.")]
        [Min(0)] public int CountBonusPerExtra = 4;

        [Tooltip("The most points the count can add.")]
        [Min(0)] public int CountBonusMax = 12;

        [Tooltip("The most kinds of animal one picture records.")]
        [Range(1, 8)] public int MaxSubjectsPerPhoto = 4;

        [Tooltip("A picture scoring this or more is a fine one.")]
        [Range(0, 100)] public int GoodScore = 70;

        [Tooltip("A picture scoring this or more (and under GoodScore) is a fair one; under it, a poor one.")]
        [Range(0, 100)] public int FairScore = 40;

        [Header("The album")]
        [Tooltip("The most pictures a player keeps. A full album takes no more.")]
        [Min(1)] public int AlbumCapacity = 60;

        [Header("The album's little Polaroids (owner 2026-11-26)")]
        [Tooltip("Width of the small picture saved with each photo, in art pixels. Drawn one kit pixel per " +
                 "pixel, point filtered. Each saved picture costs 2 + 2 x width x height bytes before Base64 " +
                 "(48 x 34: 3,266 bytes, 4,356 characters; about 255 KiB at a 60-picture album).")]
        [Range(4, 64)] public int ThumbnailWidthPx = 48;

        [Tooltip("Height of the small picture saved with each photo, in art pixels. Keep the frame's shape " +
                 "(FrameWidthMetres : FrameHeightMetres) so nothing is squashed.")]
        [Range(4, 64)] public int ThumbnailHeightPx = 34;

        [Tooltip("The Polaroid's white border on the left, right and top, in pixels.")]
        [Range(0, 8)] public int PolaroidBorderPx = 4;

        [Tooltip("The Polaroid's wider bottom border, in pixels.")]
        [Range(0, 16)] public int PolaroidBottomBorderPx = 12;

        [Tooltip("The Polaroid's frame colour.")]
        public Color32 PolaroidFrameColour = new Color32(244, 241, 232, 255);

        [Tooltip("The least air between two Polaroids side by side on a page, in pixels. The page fits as " +
                 "many across as this leaves room for, all straight (never tilted).")]
        [Range(0, 64)] public int PolaroidMinGapPx = 12;

        [Tooltip("Ruled lines left empty under each row of Polaroids and their captions. 0 fits two rows of " +
                 "48 x 34 cards to a 12-line leaf.")]
        [Range(0, 4)] public int AlbumAirRows = 0;

        [Tooltip("Clear pixels between a Polaroid's foot and the writing under it, so the words never touch " +
                 "the card. The first caption line is written on the card's bottom strip when the strip holds " +
                 "a line of writing with a clear pixel above and below.")]
        [Range(1, 4)] public int PolaroidCaptionGapPx = 1;

        [Tooltip("The kinds of animal the album can name.")]
        public List<PhotoSubjectEntry> Subjects = new List<PhotoSubjectEntry>();

        [Header("Words")]
        [Tooltip("The pose words, in WildlifePose order.")]
        public string[] PoseWords = { "resting", "watching you", "sliding in", "swimming", "diving" };

        public string GoodWord = "a fine picture";
        public string FairWord = "a fair picture";
        public string PoorWord = "a poor picture";

        [Tooltip("Under the frame while the camera is up, keyboard.")]
        public string KeyboardHint = "Space: take the picture   P: lower the camera";

        [Tooltip("Under the frame while the camera is up, gamepad.")]
        public string GamepadHint = "RT: take the picture   Y: lower the camera";

        [Tooltip("When the shutter goes on nothing the album names. Nothing is kept.")]
        public string NothingInFrameLine = "Nothing there worth the film.";

        [Tooltip("When the album is full. Nothing is kept.")]
        public string AlbumFullLine = "The album is full.";

        [Tooltip("When P is pressed with something in his hands: the camera needs both.")]
        public string HandsFullLine = "Put down what you are holding first.";

        [Tooltip("When a picture is kept. {0} is what is in it, {1} how good it is.")]
        public string TakenFormat = "{0}: {1}.";

        [Tooltip("The album page's title.")]
        public string AlbumTitle = "Pictures";

        [Tooltip("The album page while it holds nothing.")]
        public string AlbumEmptyLine = "No pictures yet.";

        [Tooltip("A picture's when-and-where line. {0} day number, {1} hour:minute, {2} the place.")]
        public string AlbumWhenFormat = "Day {0}, {1}, {2}";

        [Tooltip("A picture's heading in the album. {0} its number.")]
        public string AlbumHeadFormat = "Picture {0}";

        [Tooltip("One kind of animal in a picture, in the album. {0} how many and what, {1} the pose word.")]
        public string AlbumSubjectFormat = "{0}, {1}";

        [Tooltip("The second, small line under a Polaroid. {0} day number, {1} the place. Keep it short: it " +
                 "wraps at spaces only, never inside a word.")]
        public string AlbumCaptionWhenFormat = "Day {0}, {1}";

        [Tooltip("What the album calls each region a picture was taken in. A region not listed is named " +
                 "from its id.")]
        public List<PhotoPlaceEntry> Places = new List<PhotoPlaceEntry>();

        // ---- reads ---------------------------------------------------------------------------------

        /// <summary>The album entry for <paramref name="subjectId"/>, or null when the camera has none.</summary>
        public PhotoSubjectEntry Subject(string subjectId)
        {
            if (Subjects == null || string.IsNullOrEmpty(subjectId)) return null;
            for (int i = 0; i < Subjects.Count; i++)
                if (Subjects[i] != null && string.Equals(Subjects[i].SubjectId, subjectId, StringComparison.Ordinal))
                    return Subjects[i];
            return null;
        }

        /// <summary>The album's name for a region (its listed name, else one made from the id).</summary>
        public string PlaceName(string regionId)
        {
            if (Places != null && !string.IsNullOrEmpty(regionId))
                for (int i = 0; i < Places.Count; i++)
                    if (Places[i] != null && string.Equals(Places[i].RegionId, regionId, StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(Places[i].Name))
                        return Places[i].Name;
            return PlainName.FromId(regionId);
        }

        /// <summary>True when the album can name <paramref name="subjectId"/>.</summary>
        public bool Knows(string subjectId) => Subject(subjectId) != null;

        /// <summary>"grey seal", "3 grey seals"; the id itself for a subject the camera cannot name.</summary>
        public string CountedName(string subjectId, int count)
        {
            PhotoSubjectEntry s = Subject(subjectId);
            if (s == null) return subjectId ?? "";
            if (count <= 1) return s.DisplayName;
            string plural = string.IsNullOrEmpty(s.PluralName) ? s.DisplayName : s.PluralName;
            return count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + plural;
        }

        /// <summary>How good a picture <paramref name="pose"/> makes (0..1); 0 for a pose the table lacks.</summary>
        public float QualityOf(WildlifePose pose)
        {
            int i = (int)pose;
            return PoseQuality != null && i >= 0 && i < PoseQuality.Length ? Mathf.Clamp01(PoseQuality[i]) : 0f;
        }

        /// <summary>The pose word; empty for a pose the table lacks.</summary>
        public string PoseWord(WildlifePose pose)
        {
            int i = (int)pose;
            return PoseWords != null && i >= 0 && i < PoseWords.Length ? PoseWords[i] ?? "" : "";
        }

        /// <summary>The word for a picture's score.</summary>
        public string QualityWord(int score)
            => score >= GoodScore ? GoodWord : score >= FairScore ? FairWord : PoorWord;

        /// <summary>The whole Polaroid's width in pixels: the picture and its side borders.</summary>
        public int PolaroidWidthPx => ThumbnailWidthPx + 2 * PolaroidBorderPx;

        /// <summary>The whole Polaroid's height in pixels: the top border, the picture and the wider bottom.</summary>
        public int PolaroidHeightPx => PolaroidBorderPx + ThumbnailHeightPx + PolaroidBottomBorderPx;

        /// <summary>What one saved picture's thumbnail adds to the save, in Base64 characters.</summary>
        public int ThumbnailEncodedLength => PhotoThumbnail.EncodedLength(ThumbnailWidthPx, ThumbnailHeightPx);

        /// <summary>The shipped camera, or null when the build carries none.</summary>
        public static PhotoCameraDef Load() => Resources.Load<PhotoCameraDef>(ResourcesPath);

        /// <summary>Null when the def is whole; otherwise the first thing wrong with it.</summary>
        public string ValidationError()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith(IdPrefix, StringComparison.Ordinal) ||
                Id.Length == IdPrefix.Length)
                return "Expected a photo_camera.* id.";
            if (string.IsNullOrWhiteSpace(DisplayName)) return "The camera has no name.";
            if (MaxReachMetres < StartReachMetres) return "The frame starts farther out than it can reach.";
            if (FarDistanceMetres <= FullQualityDistanceMetres) return "FarDistance must be past FullQualityDistance.";
            if (FairScore > GoodScore) return "A fair picture cannot need more than a fine one.";
            if (ThumbnailWidthPx < 1 || ThumbnailHeightPx < 1 ||
                ThumbnailWidthPx > PhotoThumbnail.MaxSide || ThumbnailHeightPx > PhotoThumbnail.MaxSide)
                return "The album's thumbnail size is out of range.";
            if (PolaroidBorderPx < 0 || PolaroidBottomBorderPx < PolaroidBorderPx)
                return "A Polaroid's bottom border is the wider one.";
            if (PolaroidMinGapPx < 0 || AlbumAirRows < 0) return "The album's spacing cannot be negative.";
            if (PolaroidCaptionGapPx < 1) return "The writing under a Polaroid needs at least one clear pixel.";
            if (PoseQuality == null || PoseQuality.Length != PoseCount) return "PoseQuality needs one value per pose.";
            if (PoseWords == null || PoseWords.Length != PoseCount) return "PoseWords needs one word per pose.";
            for (int i = 0; i < PoseCount; i++)
                if (string.IsNullOrWhiteSpace(PoseWords[i])) return "A pose word is empty.";
            if (Subjects == null || Subjects.Count == 0) return "The camera names no subject.";
            for (int i = 0; i < Subjects.Count; i++)
            {
                PhotoSubjectEntry s = Subjects[i];
                if (s == null || string.IsNullOrWhiteSpace(s.SubjectId)) return "A subject has no id.";
                if (string.IsNullOrWhiteSpace(s.DisplayName)) return "Subject " + s.SubjectId + " has no name.";
                for (int j = 0; j < i; j++)
                    if (Subjects[j] != null && Subjects[j].SubjectId == s.SubjectId)
                        return "Subject " + s.SubjectId + " is listed twice.";
            }
            foreach (string line in new[] { GoodWord, FairWord, PoorWord, KeyboardHint, GamepadHint,
                                             NothingInFrameLine, AlbumFullLine, HandsFullLine, TakenFormat, AlbumTitle,
                                             AlbumEmptyLine, AlbumWhenFormat, AlbumHeadFormat,
                                             AlbumSubjectFormat, AlbumCaptionWhenFormat })
                if (string.IsNullOrWhiteSpace(line)) return "A word of the camera is empty.";
            return null;
        }
    }
}
