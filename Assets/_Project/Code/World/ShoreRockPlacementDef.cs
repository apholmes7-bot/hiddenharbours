using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>WHERE A REGION'S SHORE ROCKS STAND, AS THE SCENE KEEPS THEM.</b> The shore painter lays its
    /// rocks from this list instead of from its scatter. A region that cannot be rebuilt (St Peters)
    /// keeps hand work in its scene, so the scene is the truth and the painter has to agree with it:
    /// the scatter's answer moves whenever the ground under it changes.
    ///
    /// <para>Each entry is a rock's object name (<c>Rock_bs</c>, <c>FieldRock_bs</c>: prefix, then the
    /// shoreline sheet's key) and its exact place in world metres, in the scene's own child order.
    /// Exact means the scene's own serialized floats, so a refresh that lays this list is a no-op on
    /// the rocks the scene holds. What a rock looks like is its <see cref="ShoreRockDef"/>'s business.</para>
    ///
    /// <para>Plain data, no behaviour: the World module holds it and the App editor steps read it.
    /// Nothing at runtime reads it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Shore Rock Placement", fileName = "ShoreRockPlacement")]
    public class ShoreRockPlacementDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (rockplacement.<region>, e.g. rockplacement.st_peters).")]
        public string Id = "rockplacement.example";

        [Header("The rocks, in the scene's child order")]
        public List<Rock> Rocks = new List<Rock>();

        [Serializable]
        public struct Rock
        {
            [Tooltip("The rock's object name: Rock_<key> or FieldRock_<key>, key from the shoreline sheet.")]
            public string Name;

            [Tooltip("Where it stands, in world metres, exactly as the scene holds it.")]
            public Vector2 At;
        }
    }
}
