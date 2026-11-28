using System.Collections.Generic;
using System.Globalization;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The album page's words</b> (save v28): each kept picture, newest first, as its little Polaroid with
    /// a two-line caption under it (what and how many; the day and place), and as a heading and a few lines
    /// for a page drawn without Polaroids. Every word and format is the camera's def
    /// (<see cref="PhotoCameraDef"/>); this only puts them together. Pure, so the page is proven headless.
    /// </summary>
    public static class NotebookPhotoPage
    {
        /// <summary>
        /// <paramref name="playerKey"/>'s pictures, newest first, worded by <paramref name="def"/>.
        /// <paramref name="secondsPerDay"/> turns a picture's game time into its day and hour. An empty
        /// album, a null save or no def is an empty list (the page then has no stub).
        /// </summary>
        public static List<NotebookPhotoEntry> Entries(SaveData save, string playerKey, PhotoCameraDef def,
                                                       float secondsPerDay)
        {
            var entries = new List<NotebookPhotoEntry>();
            if (save?.Photos == null || def == null) return entries;
            string key = SavePlayerKeys.Normalise(playerKey);

            for (int i = save.Photos.Count - 1; i >= 0; i--)
            {
                PhotoRecordDto p = save.Photos[i];
                if (p == null || SavePlayerKeys.Normalise(p.PlayerKey) != key || p.Subjects == null) continue;

                var lines = new List<string>();
                for (int s = 0; s < p.Subjects.Count; s++)
                {
                    PhotoSubjectDto sub = p.Subjects[s];
                    if (sub == null) continue;
                    lines.Add(string.Format(CultureInfo.InvariantCulture, def.AlbumSubjectFormat,
                                            Capitalised(def.CountedName(sub.SubjectId, sub.Count)),
                                            def.PoseWord((WildlifePose)sub.Pose)));
                }
                lines.Add(Capitalised(def.QualityWord(p.Score)));
                lines.Add(WhenLine(def, p.GameTimeSeconds, p.RegionId, secondsPerDay));

                // The little Polaroid (owner 2026-11-26): the frame's size is the def's now; the picture inside
                // is drawn at its own stored size, so a retune never stretches an old picture.
                entries.Add(new NotebookPhotoEntry(
                    p.Number,
                    string.Format(CultureInfo.InvariantCulture, def.AlbumHeadFormat, p.Number),
                    lines,
                    Caption(def, p),
                    CaptionWhen(def, p.GameTimeSeconds, p.RegionId, secondsPerDay),
                    PhotoThumbnail.IsValid(p.Thumbnail) ? p.Thumbnail : "",
                    Style(def)));
            }
            return entries;
        }

        /// <summary>How the album draws its Polaroids, from <paramref name="def"/>.</summary>
        public static NotebookPolaroidStyle Style(PhotoCameraDef def) =>
            def == null
                ? default
                : new NotebookPolaroidStyle(def.PolaroidWidthPx, def.PolaroidHeightPx, def.PolaroidBorderPx,
                                            def.PolaroidBottomBorderPx, def.PolaroidFrameColour,
                                            def.PolaroidMinGapPx, def.AlbumAirRows, def.PolaroidCaptionGapPx);

        /// <summary>The first line under a Polaroid: the kind it holds most of, counted ("4 grey seals",
        /// "Grey seal"); the first listed wins a tie. A picture naming nothing is captioned by its quality.</summary>
        public static string Caption(PhotoCameraDef def, PhotoRecordDto p)
        {
            if (def == null || p == null) return "";
            PhotoSubjectDto best = null;
            if (p.Subjects != null)
                for (int s = 0; s < p.Subjects.Count; s++)
                {
                    PhotoSubjectDto sub = p.Subjects[s];
                    if (sub != null && !string.IsNullOrEmpty(sub.SubjectId) && (best == null || sub.Count > best.Count))
                        best = sub;
                }
            return Capitalised(best != null ? def.CountedName(best.SubjectId, best.Count) : def.QualityWord(p.Score));
        }

        /// <summary>The second line under a Polaroid: "Day 1, St Peters", the day counted from 1.</summary>
        public static string CaptionWhen(PhotoCameraDef def, double gameSeconds, string regionId, float secondsPerDay)
        {
            if (def == null) return "";
            double spd = secondsPerDay > 0f ? secondsPerDay : 1d;
            double t = gameSeconds > 0d ? gameSeconds : 0d;
            long day = (long)System.Math.Floor(t / spd);
            return string.Format(CultureInfo.InvariantCulture, def.AlbumCaptionWhenFormat, day + 1,
                                 def.PlaceName(regionId));
        }

        /// <summary>"Day 3, 14:20, St Peters": the day counted from 1, the hour on a 24-hour clock.</summary>
        public static string WhenLine(PhotoCameraDef def, double gameSeconds, string regionId, float secondsPerDay)
        {
            if (def == null) return "";
            double spd = secondsPerDay > 0f ? secondsPerDay : 1d;
            double t = gameSeconds > 0d ? gameSeconds : 0d;
            long day = (long)System.Math.Floor(t / spd);
            double minutes = (t - day * spd) / spd * 24d * 60d;
            int m = (int)System.Math.Floor(minutes);
            if (m >= 24 * 60) m = 24 * 60 - 1;
            string clock = (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
                           (m % 60).ToString("00", CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, def.AlbumWhenFormat, day + 1, clock,
                                 def.PlaceName(regionId));
        }

        private static string Capitalised(string s) =>
            string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
