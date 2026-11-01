using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using UnityEngine;

namespace HiddenHarbours.CoopSpike
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Spike/Two Boats")]
    public sealed class CoopSpikeSettings : ScriptableObject
    {
        public const string ResourcePath = "CoopSpike/Settings";
        public GameObject PlayerPrefab;
        public BoatHullDef[] Roster;
        public CharacterVisualDef Character;
        public ToolDef Rod;
        public Sprite RodSprite;
        public Vector2 HostStart = new(0, -16);
        public Vector2 ClientOffset = new(7, 0);
        public string Scene = "Greybox";
        public ushort Port = 7777;
        public BoatHullDef Hull(string id)
        {
            if (Roster != null) foreach (var hull in Roster) if (hull != null && hull.Id == id) return hull;
            return null;
        }
    }
}
