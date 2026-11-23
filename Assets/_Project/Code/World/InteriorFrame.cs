using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The frame a room's footprint is described in, when its doorway is on a side wall.
    ///
    /// <para><see cref="InteriorFootprint"/> and <see cref="BuildingInterior"/> put a doorway on the
    /// <c>±y</c> wall only. A room whose doorway is on <c>±x</c> (the harbour gable's, under a door in
    /// the cross gable on its long wall) is the same rectangle described from a frame turned a quarter:
    /// width and length swap, the doorway lands on <c>+y</c>, and the facing moves by a quarter of the
    /// turntable. The world rectangle, the walls and the doorway all stay where they were.</para>
    ///
    /// <para>Only the footprint's frame turns. The room's sprite keeps its own facing.</para>
    /// </summary>
    public static class InteriorFrame
    {
        /// <summary>A room's footprint frame: what <see cref="InteriorFootprint"/> and
        /// <see cref="BuildingInterior.Configure"/> take.</summary>
        public readonly struct Frame
        {
            public readonly float WidthMetres, LengthMetres;
            public readonly int Facing;
            public readonly float DoorSign, DoorAcrossMetres;

            public Frame(float widthMetres, float lengthMetres, int facing, float doorSign, float doorAcrossMetres)
            {
                WidthMetres = widthMetres;
                LengthMetres = lengthMetres;
                Facing = facing;
                DoorSign = doorSign;
                DoorAcrossMetres = doorAcrossMetres + 0f;   // never -0: it serializes as "-0"
            }
        }

        /// <summary>
        /// The frame for a room <paramref name="widthMetres"/> × <paramref name="lengthMetres"/> drawn at
        /// <paramref name="facing"/>, whose doorway is at <paramref name="doorModelMetres"/> in the room's
        /// own model frame.
        ///
        /// <para>The wall is the one the doorway is nearest, each axis taken against its half-wall. A
        /// doorway on <c>±y</c> comes back unchanged. On <c>+x</c> the frame turns so that model
        /// <c>+x</c> is its <c>+y</c>: (x, y) → (−y, x), facing + a quarter. On <c>−x</c>: (x, y) →
        /// (y, −x), facing − a quarter.</para>
        ///
        /// <para>Needs a facing count that is a multiple of four to turn by a quarter; any other count
        /// is returned unturned.</para>
        /// </summary>
        public static Frame For(float widthMetres, float lengthMetres, int facing, int facings, Vector2 doorModelMetres)
        {
            float hw = Mathf.Max(1e-4f, widthMetres * 0.5f), hl = Mathf.Max(1e-4f, lengthMetres * 0.5f);
            bool side = Mathf.Abs(doorModelMetres.x) / hw > Mathf.Abs(doorModelMetres.y) / hl;
            if (!side || facings <= 0 || facings % 4 != 0)
                return new Frame(widthMetres, lengthMetres, facing,
                                 doorModelMetres.y >= 0f ? 1f : -1f, doorModelMetres.x);

            int quarter = facings / 4;
            if (doorModelMetres.x > 0f)
                return new Frame(lengthMetres, widthMetres, Wrap(facing + quarter, facings), 1f, -doorModelMetres.y);
            return new Frame(lengthMetres, widthMetres, Wrap(facing - quarter, facings), 1f, doorModelMetres.y);
        }

        static int Wrap(int facing, int facings) => ((facing % facings) + facings) % facings;
    }
}
