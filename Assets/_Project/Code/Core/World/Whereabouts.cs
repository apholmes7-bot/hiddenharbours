using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>WHERE SOMETHING IS in the world</b>: the region it is in and its pose in that region's own
    /// local frame (the frame the region's scene is built in). The contract the save joins to "a sleep is a
    /// save" (owner ruling 2026-11-15): the player and the player's boat are each one of these.
    ///
    /// <para>Plain serializable fields (JsonUtility-friendly, the shape <c>SaveData</c> already uses for the
    /// rest anchor), so the save can store it as-is. Local, not world, coordinates: a region's scene is
    /// built in local metres, and the world frame (<see cref="WorldFrame"/>) can convert when needed —
    /// keeping the stored number in the frame the scene uses means a later layout change never moves a
    /// saved boat inside her own harbour.</para>
    ///
    /// <para><b>EMPTY <see cref="RegionId"/> means unknown</b> (never placed, or a rig with no regions).</para>
    /// </summary>
    [Serializable]
    public struct Whereabouts
    {
        /// <summary>Stable region id (<c>region.st_peters</c>); empty = unknown.</summary>
        public string RegionId;
        /// <summary>Local X (m) in that region's scene frame.</summary>
        public float X;
        /// <summary>Local Y (m) in that region's scene frame.</summary>
        public float Y;
        /// <summary>Heading, degrees about Z (the transform's Z euler angle).</summary>
        public float HeadingDegrees;

        public Whereabouts(string regionId, Vector2 local, float headingDegrees)
        {
            RegionId = regionId ?? "";
            X = local.x;
            Y = local.y;
            HeadingDegrees = headingDegrees;
        }

        public bool IsKnown => !string.IsNullOrEmpty(RegionId);
        public Vector2 LocalPosition => new Vector2(X, Y);

        /// <summary>The same place in the world frame, when the region is in it.</summary>
        public bool TryGetWorld(WorldFrame frame, out Vector2 world)
        {
            world = default;
            if (frame == null || !frame.TryGet(RegionId, out RegionFrame f)) return false;
            world = f.ToWorld(LocalPosition);
            return true;
        }

        public override string ToString() => IsKnown ? $"{RegionId} ({X:0.##}, {Y:0.##}) {HeadingDegrees:0.#}°" : "(unknown)";
    }

    /// <summary>
    /// The live answer to "where are the player and the player's boat" — kept by the persistent travel
    /// rig (App: <c>RegionTravelCoordinator</c>) and published on <see cref="GameServices.Whereabouts"/>.
    ///
    /// <para><b>The boat is in exactly one region.</b> A foot crossing never moves her; while the player is
    /// in another region she is simply not present there (her object is inactive) and she reappears where
    /// she was left when the player comes back (owner ruling 2026-11-15, "doors do not teleport").</para>
    ///
    /// <para>FLAG lead-architect: new Core contract (ADR 0057). The save lane joins here: write
    /// <see cref="Player"/> and <see cref="Boat"/> into the save; on load, hand the boat back through
    /// <see cref="RestoreBoat"/> before the first arrival.</para>
    /// </summary>
    public interface IWorldWhereabouts
    {
        /// <summary>The player, in the region they are standing (or sailing) in.</summary>
        Whereabouts Player { get; }
        /// <summary>The player's boat, in the region she was last left in.</summary>
        Whereabouts Boat { get; }
        /// <summary>Put the boat back where a save says she was. A boat whose region is not the current
        /// one stays out of sight until the player enters that region.</summary>
        void RestoreBoat(Whereabouts boat);
    }
}
