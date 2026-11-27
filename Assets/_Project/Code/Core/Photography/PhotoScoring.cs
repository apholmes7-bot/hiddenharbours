using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One kind of animal in a picture: which kind, how many, what the best of them was doing and how good
    /// a picture of them it is. A mutable value so the scoring can fill a caller's array without allocating.
    /// </summary>
    public struct PhotoSubjectResult
    {
        /// <summary>The subject id (<c>wildlife.grey_seal</c>).</summary>
        public string SubjectId;

        /// <summary>How many of this kind stood in the frame.</summary>
        public int Count;

        /// <summary>What the best-placed one was doing.</summary>
        public WildlifePose Pose;

        /// <summary>How sharp the best-placed one was (0..1), before the count bonus.</summary>
        public float Quality;

        /// <summary>The picture's score for this kind, 0..100.</summary>
        public int Score;
    }

    /// <summary>
    /// <b>HOW A PICTURE IS JUDGED</b>: pure, deterministic and allocation-free (rules 5 and 7). Everything
    /// it weighs comes from the frame, where the camera stands, what the wildlife seam reports and the
    /// numbers in <see cref="PhotoCameraDef"/>; there is no randomness and nothing is read from the clock.
    ///
    /// <para>An animal's quality is pose x distance x centring: a resting seal near the middle of a frame
    /// taken from close by is the best picture; a diving one is no picture at all. A kind's score is its
    /// best animal's quality as 0..100, plus a small bonus for every extra animal of that kind in the
    /// frame. A picture's score is its best kind's.</para>
    /// </summary>
    public static class PhotoScoring
    {
        /// <summary>The patch of world a picture takes in, centred on <paramref name="centre"/>.</summary>
        public static Rect FrameAt(PhotoCameraDef def, Vector2 centre)
        {
            float w = def != null ? def.FrameWidthMetres : 0f;
            float h = def != null ? def.FrameHeightMetres : 0f;
            return new Rect(centre.x - w * 0.5f, centre.y - h * 0.5f, w, h);
        }

        /// <summary>The aim offset (frame centre less where he stands), held inside the camera's reach.</summary>
        public static Vector2 ClampAim(PhotoCameraDef def, Vector2 offset)
        {
            float max = def != null ? def.MaxReachMetres : 0f;
            float m = offset.magnitude;
            return m > max && m > 0f ? offset * (max / m) : offset;
        }

        /// <summary>How sharp an animal <paramref name="distance"/> metres away is (0..1).</summary>
        public static float DistanceQuality(PhotoCameraDef def, float distance)
        {
            if (def == null) return 0f;
            if (distance <= def.FullQualityDistanceMetres) return 1f;
            if (distance >= def.FarDistanceMetres) return def.FarQuality;
            float t = (distance - def.FullQualityDistanceMetres) /
                      (def.FarDistanceMetres - def.FullQualityDistanceMetres);
            return Mathf.Lerp(1f, def.FarQuality, t);
        }

        /// <summary>How much an animal at <paramref name="at"/> keeps for where it stands in the frame: 1 in
        /// the middle, 1 - CentreWeight at the edge (0..1).</summary>
        public static float CentreQuality(PhotoCameraDef def, Rect frame, Vector2 at)
        {
            if (def == null || frame.width <= 0f || frame.height <= 0f) return 0f;
            float dx = Mathf.Abs(at.x - frame.center.x) / (frame.width * 0.5f);
            float dy = Mathf.Abs(at.y - frame.center.y) / (frame.height * 0.5f);
            float off = Mathf.Clamp01(Mathf.Max(dx, dy));
            return 1f - def.CentreWeight * off;
        }

        /// <summary>One animal's quality in this frame from where the camera stands (0..1).</summary>
        public static float SightingQuality(PhotoCameraDef def, Vector2 cameraAt, Rect frame, in WildlifeSighting s)
        {
            if (def == null || !s.Visible) return 0f;
            return def.QualityOf(s.Pose)
                   * DistanceQuality(def, Vector2.Distance(cameraAt, s.WorldPosition))
                   * CentreQuality(def, frame, s.WorldPosition);
        }

        /// <summary>A kind's score from its best quality and how many there were (0..100).</summary>
        public static int ScoreOf(PhotoCameraDef def, float bestQuality, int count)
        {
            if (def == null) return 0;
            int bonus = Mathf.Min(def.CountBonusMax, def.CountBonusPerExtra * Mathf.Max(0, count - 1));
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(bestQuality) * 100f) + bonus, 0, 100);
        }

        /// <summary>
        /// Judge a picture. Every visible animal of a kind the camera can name, standing inside
        /// <paramref name="frame"/>, is grouped by kind into <paramref name="into"/>, which comes back
        /// ordered best score first (ties by subject id, so the order is the same on every machine). At most
        /// <see cref="PhotoCameraDef.MaxSubjectsPerPhoto"/> kinds and <c>into.Length</c> are kept; a kind
        /// met once the buffer is full is left out. Returns how many kinds were written. Allocates nothing.
        /// </summary>
        public static int Evaluate(PhotoCameraDef def, Vector2 cameraAt, Rect frame,
                                   WildlifeSighting[] sightings, int count, PhotoSubjectResult[] into)
        {
            if (def == null || sightings == null || into == null) return 0;
            int cap = Mathf.Min(into.Length, def.MaxSubjectsPerPhoto);
            int kinds = 0;
            int n = Mathf.Min(count, sightings.Length);
            for (int i = 0; i < n; i++)
            {
                WildlifeSighting s = sightings[i];
                if (!s.Visible || !frame.Contains(s.WorldPosition) || !def.Knows(s.SubjectId)) continue;
                float q = SightingQuality(def, cameraAt, frame, s);

                int k = 0;
                while (k < kinds && !string.Equals(into[k].SubjectId, s.SubjectId, StringComparison.Ordinal)) k++;
                if (k == kinds)
                {
                    if (kinds >= cap) continue;
                    into[k] = new PhotoSubjectResult { SubjectId = s.SubjectId, Count = 0, Pose = s.Pose, Quality = q };
                    kinds++;
                }
                into[k].Count++;
                // The best-placed animal sets the pose; a tie keeps the pose that is the better picture
                // (the lower WildlifePose), so the order the haul-outs list them in never matters.
                if (q > into[k].Quality || (q == into[k].Quality && s.Pose < into[k].Pose))
                {
                    into[k].Quality = q;
                    into[k].Pose = s.Pose;
                }
            }

            for (int k = 0; k < kinds; k++) into[k].Score = ScoreOf(def, into[k].Quality, into[k].Count);

            // Insertion sort: a handful of kinds at most, and no allocation.
            for (int a = 1; a < kinds; a++)
            {
                PhotoSubjectResult x = into[a];
                int b = a - 1;
                while (b >= 0 && Before(x, into[b])) { into[b + 1] = into[b]; b--; }
                into[b + 1] = x;
            }
            return kinds;
        }

        /// <summary>A picture's score: its best kind's (0 for a picture of nothing).</summary>
        public static int PhotoScore(PhotoSubjectResult[] results, int count)
        {
            int best = 0;
            if (results == null) return 0;
            for (int i = 0; i < Mathf.Min(count, results.Length); i++)
                if (results[i].Score > best) best = results[i].Score;
            return best;
        }

        private static bool Before(in PhotoSubjectResult a, in PhotoSubjectResult b)
        {
            if (a.Score != b.Score) return a.Score > b.Score;
            return string.CompareOrdinal(a.SubjectId, b.SubjectId) < 0;
        }
    }
}
