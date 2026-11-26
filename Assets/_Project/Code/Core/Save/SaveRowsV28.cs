using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One picture in a player's album, as saved</b> (save v28; owner 2026-11-26: "build the camera now
    /// with photo record added to save"). One row per picture kept.
    ///
    /// <para><b>A record, never an image.</b> The row says what the picture was of and how good it was: which
    /// kinds of animal stood in the frame, how many, what they were doing, the score, when and where. No pixel
    /// is saved; the album draws each picture from these words. A thumbnail is an owner question, not built.</para>
    ///
    /// <para>The score is SAVED, not recomputed: a picture is what it was when the shutter went, and a later
    /// retune of the camera's numbers must not turn a picture an islander already thanked him for into a poor
    /// one. Nothing about the tide, the wind or the weather is kept (rule 5).</para>
    ///
    /// <para>Read and written through <see cref="PhotoAlbumRows"/>. Flat and JsonUtility-friendly.</para>
    /// </summary>
    [Serializable]
    public sealed class PhotoRecordDto
    {
        /// <summary>Whose album the picture is in (<see cref="SavePlayerKeys"/>).</summary>
        public string PlayerKey = SavePlayerKeys.Local;

        /// <summary>The picture's number, unique in the save, from 1 and only ever going up (a counter worked
        /// out from the rows, never a random id: rule 5). The album lists pictures in this order.</summary>
        public int Number;

        /// <summary>The game clock (<c>IGameClock.TotalSeconds</c>) when the shutter went.</summary>
        public double GameTimeSeconds;

        /// <summary>The region the picture was taken in (<c>GameServices.CurrentRegionId</c>).</summary>
        public string RegionId = "";

        /// <summary>The picture's score, 0..100: its best subject's.</summary>
        public int Score;

        /// <summary>The kinds of animal in the picture, best first. A kept picture has at least one.</summary>
        public List<PhotoSubjectDto> Subjects = new List<PhotoSubjectDto>();
    }

    /// <summary>
    /// <b>One kind of animal in a saved picture</b> (save v28): which kind, how many stood in the frame, what
    /// the best-placed one was doing and the score for that kind.
    /// </summary>
    [Serializable]
    public sealed class PhotoSubjectDto
    {
        /// <summary>The subject's stable id: its wildlife species Def id (<c>wildlife.grey_seal</c>).</summary>
        public string SubjectId = "";

        /// <summary>How many of this kind stood in the frame (at least 1 on a kept row).</summary>
        public int Count = 1;

        /// <summary>What the best-placed one was doing: the <see cref="WildlifePose"/> as its stable integer
        /// (stored flat, the <see cref="RadarPrefsDto.Mode"/> reason; the load repair holds it in range).</summary>
        public int Pose;

        /// <summary>The score for this kind, 0..100.</summary>
        public int Score;
    }
}
