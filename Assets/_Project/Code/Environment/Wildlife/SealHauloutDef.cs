using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>One rock place a seal lies on.</summary>
    [Serializable]
    public sealed class SealSpot
    {
        [Tooltip("Stable within the haul-out: every hash about this seal is keyed by it. The St Peters spots keep the key scene's row ids.")]
        public string Id = "";

        [Tooltip("Where the seal lies, world metres (the key-scene frame): the point its belly rests on.")]
        public Vector2 At;

        [Tooltip("The height (m above chart datum) of the rock under it. The spot is usable only while the water is below this by the species' margins.")]
        public float LieMetres;

        [Tooltip("Which way it lies: the key scene's dir, 0 N then COUNTERCLOCKWISE (1 NW, 2 W ... 4 S ... 6 E, 7 NE). East-ish faces right, west-ish left.")]
        [Range(0, 7)] public int Facing;

        [Tooltip("Index into the species' coats.")]
        [Min(0)] public int Coat;

        [Tooltip("How far (m) south of At the seal's sort line sits: put it at or past the foot of the rock it lies on, so it draws over that rock.")]
        [Min(0f)] public float SortBelowMetres;
    }

    /// <summary>
    /// ONE HAUL-OUT — the rocks on a reef that seals lie on, and the open water they swim off into. One
    /// asset per place (rule 2) under <c>Data/Wildlife</c>; the first is
    /// <c>haulout.st_peters.whelps</c>, the Whelps reef below the keeper's cottage.
    ///
    /// <para><b>Ids are permanent once merged.</b> Every hash about a seal is keyed by this id and its
    /// spot's id, so renaming one moves every seal it has.</para>
    ///
    /// <para><b>Content, never state.</b> Nothing here enters the save; which spots are used is a
    /// function of these numbers, the seed, the game time and the tide (rule 5).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Seal Haul-out", fileName = "SealHaulout")]
    public sealed class SealHauloutDef : ScriptableObject
    {
        /// <summary>The id prefix of a haul-out.</summary>
        public const string IdPrefix = "haulout.";

        /// <summary>The most spots one haul-out may author (the content test holds every haul-out to it).</summary>
        public const int MaxSpots = 32;

        [Tooltip("haulout.<region>.<place>. Permanent once merged.")]
        public string Id = "";

        public string DisplayName = "";

        [Tooltip("The region it belongs to (region.st_peters).")]
        public string RegionId = "";

        [Tooltip("The key scene its spots were taken from, when they were (the content test pins them to its rows).")]
        public string KeySceneId = "";

        [Tooltip("The animal that uses it.")]
        public WildlifeSpeciesDef Species;

        [Tooltip("The rock places.")]
        public List<SealSpot> Spots = new List<SealSpot>();

        [Tooltip("Open water off the reef, world metres: a seal off its rock swims towards the nearest.")]
        public Vector2[] SwimPoints = Array.Empty<Vector2>();

        /// <summary>The swim point nearest <paramref name="from"/>, or <paramref name="from"/> itself with none.</summary>
        public Vector2 NearestSwimPoint(Vector2 from)
        {
            if (SwimPoints == null || SwimPoints.Length == 0) return from;
            Vector2 best = SwimPoints[0];
            float bestD = (best - from).sqrMagnitude;
            for (int i = 1; i < SwimPoints.Length; i++)
            {
                float d = (SwimPoints[i] - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = SwimPoints[i]; }
            }
            return best;
        }
    }
}
