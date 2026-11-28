using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The one place a player's saved pictures are read and written</b> (save v28,
    /// <see cref="SaveData.Photos"/>). Keeps no state of its own; static and null-safe (a null save reads an
    /// empty album and a write to it does nothing).
    /// </summary>
    public static class PhotoAlbumRows
    {
        /// <summary>The number the next kept picture takes: one past the highest on any row (1 for an empty
        /// album), so no number is used twice in a save.</summary>
        public static int NextNumber(SaveData save)
        {
            int max = 0;
            if (save?.Photos != null)
                for (int i = 0; i < save.Photos.Count; i++)
                    if (save.Photos[i] != null && save.Photos[i].Number > max) max = save.Photos[i].Number;
            return max + 1;
        }

        /// <summary>How many pictures <paramref name="playerKey"/>'s album holds.</summary>
        public static int Count(SaveData save, string playerKey)
        {
            if (save?.Photos == null) return 0;
            string key = SavePlayerKeys.Normalise(playerKey);
            int n = 0;
            for (int i = 0; i < save.Photos.Count; i++)
                if (IsFor(save.Photos[i], key)) n++;
            return n;
        }

        /// <summary>
        /// Keep a picture in <paramref name="playerKey"/>'s album: <paramref name="count"/> subjects from
        /// <paramref name="subjects"/> (best first). Returns the row, or null, changing nothing, for a null
        /// save, a picture of nothing, or an album already holding <paramref name="capacity"/> pictures.
        /// Allocates the row it keeps (once per shutter press, never per frame). <paramref name="thumbnail"/>
        /// is the picture's stored small image (<see cref="PhotoThumbnail.Encode"/>); one that does not decode
        /// is not kept, and the row keeps an empty thumbnail.
        /// </summary>
        public static PhotoRecordDto Add(SaveData save, string playerKey, double gameTimeSeconds, string regionId,
                                         PhotoSubjectResult[] subjects, int count, int capacity,
                                         string thumbnail = null)
        {
            if (save == null || subjects == null || count <= 0) return null;
            if (Count(save, playerKey) >= capacity) return null;
            save.Photos ??= new List<PhotoRecordDto>();

            var row = new PhotoRecordDto
            {
                PlayerKey = SavePlayerKeys.Normalise(playerKey),
                Number = NextNumber(save),
                GameTimeSeconds = gameTimeSeconds > 0d ? gameTimeSeconds : 0d,
                RegionId = regionId ?? "",
                Thumbnail = PhotoThumbnail.IsValid(thumbnail) ? thumbnail : "",
            };
            int n = Math.Min(count, subjects.Length);
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(subjects[i].SubjectId)) continue;
                row.Subjects.Add(new PhotoSubjectDto
                {
                    SubjectId = subjects[i].SubjectId,
                    Count = Math.Max(1, subjects[i].Count),
                    Pose = (int)subjects[i].Pose,
                    Score = Clamp(subjects[i].Score),
                });
                if (subjects[i].Score > row.Score) row.Score = Clamp(subjects[i].Score);
            }
            if (row.Subjects.Count == 0) return null;
            save.Photos.Add(row);
            return row;
        }

        /// <summary>Give picture <paramref name="number"/> its small image after the shutter (the capture
        /// runs once the row exists). False, changing nothing, for no such picture or a string that does not
        /// decode.</summary>
        public static bool SetThumbnail(SaveData save, int number, string thumbnail)
        {
            PhotoRecordDto row = Find(save, number);
            if (row == null || !PhotoThumbnail.IsValid(thumbnail)) return false;
            row.Thumbnail = thumbnail;
            return true;
        }

        /// <summary>The picture numbered <paramref name="number"/>, or null.</summary>
        public static PhotoRecordDto Find(SaveData save, int number)
        {
            if (save?.Photos == null) return null;
            for (int i = 0; i < save.Photos.Count; i++)
                if (save.Photos[i] != null && save.Photos[i].Number == number) return save.Photos[i];
            return null;
        }

        /// <summary>
        /// Does <paramref name="playerKey"/>'s album hold a picture of at least <paramref name="minCount"/>
        /// <paramref name="subjectId"/>, the best of them doing nothing worse than <paramref name="worstPose"/>,
        /// scoring at least <paramref name="minScore"/> for that kind? What an islander's photo request asks.
        /// </summary>
        public static bool HasQualifying(SaveData save, string playerKey, string subjectId, int minCount,
                                         WildlifePose worstPose, int minScore)
        {
            if (save?.Photos == null || string.IsNullOrEmpty(subjectId)) return false;
            string key = SavePlayerKeys.Normalise(playerKey);
            for (int i = 0; i < save.Photos.Count; i++)
            {
                PhotoRecordDto photo = save.Photos[i];
                if (!IsFor(photo, key) || photo.Subjects == null) continue;
                for (int s = 0; s < photo.Subjects.Count; s++)
                {
                    PhotoSubjectDto subject = photo.Subjects[s];
                    if (subject == null || !string.Equals(subject.SubjectId, subjectId, StringComparison.Ordinal))
                        continue;
                    if (subject.Count >= minCount && subject.Pose <= (int)worstPose && subject.Score >= minScore)
                        return true;
                }
            }
            return false;
        }

        private static bool IsFor(PhotoRecordDto photo, string normalisedKey) =>
            photo != null && string.Equals(SavePlayerKeys.Normalise(photo.PlayerKey), normalisedKey,
                                           StringComparison.Ordinal);

        private static int Clamp(int score) => score < 0 ? 0 : score > 100 ? 100 : score;
    }
}
