using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Pure maths for where an appliance faces and where a point on it lands in the world.
    ///
    /// <para><b>The wall.</b> A piece stands with its back (+Y in its own frame) against one of the room's four
    /// walls. In the room's model frame those are <c>+y</c>, <c>+x</c>, <c>−y</c> and <c>−x</c>, and the piece is
    /// then turned 0, 2, 4 or 6 facings from the room (<see cref="WallOffsets"/>), the same turn
    /// <see cref="InteriorFootprint.PropQuad"/> takes. Its sheet cell is the room's facing plus that turn.</para>
    ///
    /// <para><b>The projection.</b> Cell <c>k</c> shows the piece yawed <c>−45°·k</c> on the ground, exactly as a
    /// room's facing does (<see cref="InteriorFootprint.YawDegrees"/>): rotate on the ground, squash the
    /// ground by <see cref="IsoGround.GroundDepthScale"/>, then lift height by <see cref="IsoGround.HeightScale"/>.</para>
    /// </summary>
    public static class ApplianceFacing
    {
        /// <summary>The turn (in facings, of 8) a piece takes against each model wall: +y, +x, −y, −x.</summary>
        public static readonly int[] WallOffsets = { 0, 2, 4, 6 };

        /// <summary>The sheet cell a piece shows against a wall, for a room showing <paramref name="roomFacing"/>.</summary>
        public static int CellFor(int roomFacing, int wallOffset, int facings = ApplianceSheets.Facings)
        {
            int n = Mathf.Max(1, facings);
            return ((roomFacing + wallOffset) % n + n) % n;
        }

        /// <summary>
        /// Which wall a piece at <paramref name="model"/> (room model metres) stands against: the wall whose
        /// distance best matches the half-depth the piece would have against it. A piece in a corner touches
        /// two walls; it backs onto the one its depth fits. Returns the turn from <see cref="WallOffsets"/>.
        /// </summary>
        public static int PickWallOffset(Vector2 model, float roomWidth, float roomLength, Vector2 foot)
        {
            float half = Mathf.Max(0f, foot.y) * 0.5f;
            // Distances from the point to the +y, +x, −y, −x walls, in WallOffsets order.
            float dPy = roomLength * 0.5f - model.y;
            float dPx = roomWidth * 0.5f - model.x;
            float dNy = roomLength * 0.5f + model.y;
            float dNx = roomWidth * 0.5f + model.x;

            int best = 0;
            float bestScore = Mathf.Abs(dPy - half);
            Consider(1, Mathf.Abs(dPx - half), ref best, ref bestScore);
            Consider(2, Mathf.Abs(dNy - half), ref best, ref bestScore);
            Consider(3, Mathf.Abs(dNx - half), ref best, ref bestScore);
            return WallOffsets[best];
        }

        private static void Consider(int index, float score, ref int best, ref float bestScore)
        {
            // Strictly better only: ties keep the earlier wall, so the answer never flickers.
            if (score < bestScore - 1e-4f) { best = index; bestScore = score; }
        }

        /// <summary>A PROP-frame point (metres, z up) as a world offset from the piece's pivot at cell
        /// <paramref name="cell"/>.</summary>
        public static Vector2 ModelToWorldOffset(int cell, Vector3 prop, int facings = ApplianceSheets.Facings)
        {
            float rad = -360f * cell / Mathf.Max(1, facings) * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            float gx = prop.x * c - prop.y * s;
            float gy = prop.x * s + prop.y * c;
            return new Vector2(gx, gy * IsoGround.GroundDepthScale + prop.z * IsoGround.HeightScale);
        }

        /// <summary>The compass bearing (0 = north, clockwise) of a PROP-frame ground direction at a cell.</summary>
        public static float BearingDegrees(int cell, Vector2 propDirection, int facings = ApplianceSheets.Facings)
            => IsoGround.BearingDegrees(ModelToWorldOffset(cell, new Vector3(propDirection.x, propDirection.y, 0f), facings));

        /// <summary>Where the figure stands to climb in: in front of the piece, level with its seat, the stand-off
        /// beyond its front edge (PROP frame, ground).</summary>
        public static Vector3 EntryProp(in ApplianceLayout layout, float standOffMetres)
            => new Vector3(layout.Seat.x, -layout.Foot.y * 0.5f - Mathf.Max(0f, standOffMetres), 0f);
    }
}
