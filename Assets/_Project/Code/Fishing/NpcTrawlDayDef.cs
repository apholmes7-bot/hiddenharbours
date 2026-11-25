using System;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>One NPC trawler's working day</b> (P3, a living working coast): where she lies, when she lets go, the
    /// way out of her harbour, the way across the open water to her ground, how long she works it, and the way
    /// home. One Def per boat (rule 2); every number she keeps is here (rule 6).
    ///
    /// <para><b>Two regions, one day.</b> Her harbour and her ground are different region scenes with their
    /// own metres. The day joins them at two points: the last point of <see cref="HarbourOut"/> (the channel
    /// mouth) and the first point of <see cref="PassageOut"/> (the seam she arrives at). She is at one or the
    /// other at the same clock second, so she is in exactly one region at every instant.</para>
    ///
    /// <para><b>Positions are copied, not looked up.</b> Fishing may not name World's berths or the scene
    /// builders' arrival tables (rule 4). The berth and the two seam points are written here and the content
    /// tests hold them equal to the committed berth and arrivals.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/NPC Trawl Day", fileName = "NewNpcTrawlDay")]
    public sealed class NpcTrawlDayDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only: trawlday.snake_case.")]
        public string Id = "trawlday.";

        [Header("Harbour (her home region's metres)")]
        [Tooltip("The scene her harbour is in. She is drawn there while the day has her in harbour.")]
        public string HarbourSceneName = "";
        [Tooltip("The berth she keeps, by BerthDef id. The content tests hold BerthPosition and " +
                 "BerthHeadingDegrees equal to that berth.")]
        public string BerthId = "berth.";
        [Tooltip("Where she lies: the middle of her berth.")]
        public Vector2 BerthPosition;
        [Tooltip("The compass heading her bow lies at (0 = North, clockwise).")]
        public float BerthHeadingDegrees;
        [Tooltip("The way out, from the berth to the channel mouth. The last point is where she leaves the " +
                 "harbour's region.")]
        public Vector2[] HarbourOut = Array.Empty<Vector2>();
        [Tooltip("How many of the first HarbourOut legs she makes stern first, holding her berth heading. " +
                 "A long hull in a narrow gut backs out; 0 = she turns and goes bow first.")]
        [Min(0)] public int SternFirstLegCount;
        [Tooltip("The way in, from the channel mouth to the berth. The first point is where she comes back " +
                 "into the harbour's region. The berth itself is added after the last point.")]
        public Vector2[] HarbourHome = Array.Empty<Vector2>();

        [Header("Ground (the open water's metres)")]
        [Tooltip("The scene her ground is in. She is drawn there while the day has her outside.")]
        public string GroundSceneName = "";
        [Tooltip("The ground she works, by FishingGroundDef id.")]
        public string GroundId = "fishing_ground.";
        [Tooltip("From the seam she arrives at to the start of her working track. The first point is where " +
                 "she appears.")]
        public Vector2[] PassageOut = Array.Empty<Vector2>();
        [Tooltip("The line she works, end to end and back again, for OnGroundHours. At least two points.")]
        public Vector2[] WorkTrack = Array.Empty<Vector2>();
        [Tooltip("From the working track back to the seam. The last point is where she leaves.")]
        public Vector2[] PassageHome = Array.Empty<Vector2>();

        [Header("Her day")]
        [Tooltip("Game hour (0-24) the crew are aboard and she starts to single up.")]
        [Range(0f, 24f)] public float DepartHour = 5f;
        [Tooltip("She leaves up to this many game minutes early or late, a different draw each day.")]
        [Min(0f)] public float DepartJitterMinutes = 15f;
        [Tooltip("Game hours she spends on the ground before she turns for home.")]
        [Min(0f)] public float OnGroundHours = 5f;

        [Header("Her way (metres per clock second)")]
        [Min(0.01f)] public float HarbourSpeed = 1.5f;
        [Min(0.01f)] public float CruiseSpeed = 5f;
        [Min(0.01f)] public float WorkSpeed = 1.8f;
        [Tooltip("Clock seconds she takes to swing onto a new leg.")]
        [Min(0f)] public float TurnSeconds = 8f;
        [Tooltip("Clock seconds alongside with the crew aboard before the lines come off.")]
        [Min(0f)] public float BoardSeconds = 20f;
        [Tooltip("Clock seconds to let go.")]
        [Min(0f)] public float LetGoSeconds = 10f;
        [Tooltip("Clock seconds to make fast.")]
        [Min(0f)] public float MakeFastSeconds = 12f;

        [Header("Water she needs")]
        [Tooltip("What she draws, metres. The content tests hold it equal to her hull's.")]
        [Min(0f)] public float DraughtMeters = 4.2f;
        [Tooltip("Water she wants under her keel on every leg at the lowest tide, metres.")]
        [Min(0f)] public float KeelClearanceMeters = 0.45f;
    }
}
