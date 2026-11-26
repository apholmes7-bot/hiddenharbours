using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    [CreateAssetMenu(menuName = "Co-op Trial/Two Boats")]
    public sealed class CoopSpikeSettings : ScriptableObject
    {
        public const string ResourcePath = "CoopSpike/Settings";
        public GameObject PlayerPrefab;
        public BoatHullDef[] Roster;
        public CharacterVisualDef Character;
        public ToolDef Rod;
        public Sprite RodSprite;
        // One starting place per participant, in metres from the region's own authored berth.
        // A session admits as many participants as there are places, and never more than MaxPlayers.
        public Vector2[] SlotOffsets = { Vector2.zero };
        [Min(1)] public int MaxPlayers = 2;
        public string Scene = "StPeters";
        public ushort Port = 7777;
        // How long a joiner waits for the host before the panel says it could not connect.
        [Min(1f)] public float JoinTimeoutSeconds = 15f;

        /// <summary>How many participants one session admits: never more than there are starting places.</summary>
        public int Capacity => Mathf.Min(MaxPlayers, SlotOffsets != null ? SlotOffsets.Length : 0);

        public Vector2 OffsetFor(int slot) =>
            SlotOffsets != null && slot >= 0 && slot < SlotOffsets.Length ? SlotOffsets[slot] : Vector2.zero;

        public BoatHullDef Hull(string id)
        {
            if (Roster != null) foreach (var hull in Roster) if (hull != null && hull.Id == id) return hull;
            return null;
        }
    }
}
