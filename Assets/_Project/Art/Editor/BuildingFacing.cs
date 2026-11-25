#if UNITY_EDITOR
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Which baked FACING turns a building's door toward a point — derived from the per-facing door
    /// anchors the bake wrote, not from arithmetic about how cells are supposed to be ordered.
    ///
    /// <para>Shared by every baked-building kit (the village houses, the shop kit) because they share
    /// one turntable and one anchor convention, and because a second copy of this reasoning is a second
    /// chance to get its sign wrong.</para>
    ///
    /// <para><b>🔴 WHY IT IS MEASURED, AND WHAT THE ARITHMETIC VERSION GOT WRONG.</b>
    /// <c>StPetersVillage.FacingToward</c> computed
    /// <c>facing = FrontFacing + round(turnFromSouth / perCell)</c>. Cell <c>i</c> is baked at
    /// <c>RigBaker.DirForCell</c>, which for a counter-clockwise rig hands the rig
    /// <c>dir = (facings − i) mod facings</c>, so the model turns <b>−45° per cell</b> and a door's
    /// ground bearing DECREASES as the cell index rises. That <c>+</c> should have been a <c>−</c>.
    /// Measured against the village kit's own baked anchors, the schoolhouse door came out <b>~92°</b>
    /// from the green it is meant to face. Its test could not see it: <c>DoorErrorDegrees</c> is the
    /// algebraic inverse of the implementation, so the two agreed with each other and with nothing
    /// else. Reading the anchors cannot make that mistake — a wrong sign would have to be a wrong
    /// sign in the baked pixels.</para>
    ///
    /// <para><b>⚠️ Two traps this file exists to keep out of its callers.</b></para>
    /// <list type="bullet">
    ///   <item><b>Un-squash before taking any angle.</b> Anchors are SCREEN offsets and the world XY
    ///   plane is the squashed ground plane (<c>sin 40° ≈ 0.643</c>), so <c>atan2</c> on raw px is not
    ///   a bearing. It does not flip a sign, but it moves an angle by up to 20° — enough to round to
    ///   the wrong cell at the diagonals, which is where four of the eight facings live.</item>
    ///   <item><b>A door is not always centred on its gable, so the bearing TO the anchor is not the
    ///   direction the door FACES.</b> Measured: the post office's street door sits 1.68 m off centre
    ///   and the restaurant's 2.52 m, which throws the anchor bearing by over 20° — a half-cell, i.e.
    ///   exactly enough to pick the neighbouring facing. So the wall NORMAL is what is derived and
    ///   compared, and the anchor is only used to find which wall the door is in.</item>
    /// </list>
    /// </summary>
    public static class BuildingFacing
    {
        /// <summary>
        /// The door's position in the building's own MODEL frame at facing 0, in metres:
        /// <c>x</c> across the front (<c>+x</c> right), <c>y</c> front-to-back. For every rig in this
        /// repo <c>|y|</c> comes out at half the footprint length, because the door is in a gable wall.
        /// </summary>
        public static Vector2 DoorModelMetres(float[] doorX, float[] doorY, float pivotX, float pivotY,
                                              float pixelsPerMetre, float depthScale)
        {
            if (doorX == null || doorY == null || doorX.Length == 0 || pixelsPerMetre <= 0f)
                return Vector2.zero;

            // Contract anchors are top-left origin, so screen-up is −y. Un-squash to reach the ground.
            float across = (doorX[0] - pivotX) / pixelsPerMetre;
            float along = -(doorY[0] - pivotY) / depthScale / pixelsPerMetre;
            return new Vector2(across, along);
        }

        /// <summary>
        /// Which model-frame wall the door is in at facing 0: <c>+1</c> for <c>+y</c>, <c>−1</c> for
        /// <c>−y</c>. Both exterior rigs in the repo put it on <c>+y</c>; <c>interiorIsoRig</c> puts its
        /// on <c>−y</c>, which is the whole reason the house family carries a facing offset and the shop
        /// family does not.
        ///
        /// <para>Takes no scale: screen-y is up-negative and the squash is a positive factor, so the SIDE
        /// is decided by a sign that neither the pixels-per-metre nor the depth scale can change. That is
        /// why the bearing table below needs neither.</para>
        /// </summary>
        public static float DoorSign(float[] doorY, float pivotY) =>
            doorY != null && doorY.Length > 0 && doorY[0] < pivotY ? 1f : -1f;

        /// <summary>
        /// The GROUND bearing the door faces at each baked facing, in degrees — the door wall's outward
        /// normal, not the bearing to the anchor.
        ///
        /// <para>Derived rather than sampled per facing on purpose: the anchor set is a rigid rotation,
        /// so one measured wall side plus the turntable's step is exact, whereas eight separate
        /// <c>atan2</c>s of an off-centre anchor are eight separately-wrong angles.</para>
        /// </summary>
        public static float[] DoorBearings(float[] doorY, float pivotY, int facings) =>
            DoorBearings(DoorSign(doorY, pivotY) > 0f ? WallPlusY : WallMinusY, facings);

        // ---- the eave door ------------------------------------------------------------------------
        //
        // A house with no porch has its door on a long (eave) wall, ±x. The harbour gable's door is in
        // a cross gable on +x. The two reads above only tell +y from −y, so an eave door would be read
        // as a gable door and turned a quarter-turn off. These read the wall first.

        /// <summary>The door is in the <c>+y</c> gable.</summary>
        public const int WallPlusY = 1;
        /// <summary>The door is in the <c>−y</c> gable.</summary>
        public const int WallMinusY = -1;
        /// <summary>The door is in the <c>+x</c> eave wall.</summary>
        public const int WallPlusX = 2;
        /// <summary>The door is in the <c>−x</c> eave wall.</summary>
        public const int WallMinusX = -2;

        /// <summary>
        /// Which model-frame wall the door is in at facing 0, one of the <c>Wall*</c> constants.
        ///
        /// <para>Screen x does not depend on height, so the door's ground position can be read from x
        /// alone: at facing 0 the anchor's x is the model x, and a quarter-turn later it is the model y
        /// (up to a sign this does not need). Each is divided by its half-wall, and the larger one names
        /// the wall. A gable door gives (0, 1); the harbour gable's eave door gives (1.18, 0).</para>
        ///
        /// <para>Falls back to <see cref="DoorSign"/> (a gable) when it cannot read the quarter-turn: no
        /// anchors, a facing count that is not a multiple of four, or no footprint.</para>
        /// </summary>
        public static int DoorWall(float[] doorX, float[] doorY, float pivotX, float pivotY, int facings,
                                   float footprintWidthMetres, float footprintLengthMetres)
        {
            if (doorX != null && facings > 0 && facings % 4 == 0 && doorX.Length >= facings &&
                footprintWidthMetres > 0f && footprintLengthMetres > 0f)
            {
                float across = Mathf.Abs(doorX[0] - pivotX) / (footprintWidthMetres * 0.5f);
                float along = Mathf.Abs(doorX[facings / 4] - pivotX) / (footprintLengthMetres * 0.5f);
                if (across > along) return doorX[0] > pivotX ? WallPlusX : WallMinusX;
            }
            return DoorSign(doorY, pivotY) > 0f ? WallPlusY : WallMinusY;
        }

        /// <summary>The ground bearing a wall's outward normal points at facing 0, in degrees: +y is
        /// north (90°), +x is east (0°).</summary>
        public static float BearingAtZero(int wall)
        {
            switch (wall)
            {
                case WallPlusX: return 0f;
                case WallMinusX: return 180f;
                case WallMinusY: return -90f;
                default: return 90f;
            }
        }

        /// <summary>The bearing table for a door in <paramref name="wall"/>.</summary>
        public static float[] DoorBearings(int wall, int facings)
        {
            if (facings <= 0) return System.Array.Empty<float>();

            float atZero = BearingAtZero(wall);
            float perCell = 360f / facings;

            var bearings = new float[facings];
            for (int i = 0; i < facings; i++)
                bearings[i] = Mathf.DeltaAngle(0f, atZero - perCell * i);
            return bearings;
        }

        /// <summary>
        /// The facing whose door points closest to <paramref name="target"/> from
        /// <paramref name="from"/>. Both points are WORLD positions, so the y difference is squashed and
        /// is un-squashed here before any angle is taken.
        ///
        /// <para>Facing-count agnostic: it compares against however many bearings the bake produced, so
        /// a re-bake at four facings lands each door on the nearest quarter-turn instead of silently
        /// pointing it at a wall.</para>
        /// </summary>
        public static int FacingToward(float[] doorY, float pivotY, int facings, float depthScale,
                                       Vector2 from, Vector2 target) =>
            FacingToward(DoorSign(doorY, pivotY) > 0f ? WallPlusY : WallMinusY, facings, depthScale, from, target);

        /// <summary><see cref="FacingToward(float[],float,int,float,Vector2,Vector2)"/> for a door in a
        /// known wall (<see cref="DoorWall"/>), so an eave door turns the right way too.</summary>
        public static int FacingToward(int wall, int facings, float depthScale, Vector2 from, Vector2 target)
        {
            if (facings <= 0) return 0;

            float[] bearings = DoorBearings(wall, facings);
            if (bearings.Length == 0) return 0;

            Vector2 d = target - from;
            if (d.sqrMagnitude < 1e-6f) return FrontFacing(bearings);

            float wanted = Mathf.Atan2(d.y / Mathf.Max(1e-6f, depthScale), d.x) * Mathf.Rad2Deg;

            int best = 0;
            float bestError = float.MaxValue;
            for (int i = 0; i < bearings.Length; i++)
            {
                float error = Mathf.Abs(Mathf.DeltaAngle(bearings[i], wanted));
                if (error < bestError) { bestError = error; best = i; }
            }
            return best;
        }

        /// <summary>
        /// How far off, in degrees, a facing points the door from the target — the honest error, for a
        /// test to assert a bound on rather than re-deriving the choice and finding it agrees with
        /// itself.
        /// </summary>
        public static float DoorErrorDegrees(float[] doorY, float pivotY, int facings, float depthScale,
                                             Vector2 from, Vector2 target, int facing) =>
            DoorErrorDegrees(DoorSign(doorY, pivotY) > 0f ? WallPlusY : WallMinusY, facings, depthScale,
                             from, target, facing);

        /// <summary>The error for a door in a known wall (<see cref="DoorWall"/>).</summary>
        public static float DoorErrorDegrees(int wall, int facings, float depthScale,
                                             Vector2 from, Vector2 target, int facing)
        {
            float[] bearings = DoorBearings(wall, facings);
            if (bearings.Length == 0) return 0f;

            Vector2 d = target - from;
            float wanted = Mathf.Atan2(d.y / Mathf.Max(1e-6f, depthScale), d.x) * Mathf.Rad2Deg;
            int i = ((facing % bearings.Length) + bearings.Length) % bearings.Length;
            return Mathf.Abs(Mathf.DeltaAngle(bearings[i], wanted));
        }

        /// <summary>The facing whose door faces the camera (bearing −90°, due south), from the derived
        /// bearing table. Unlike "the lowest door anchor" this is exact for an off-centre door — the
        /// restaurant's anchor rule picks cell 5 on a 2 px margin; its normal picks 4.</summary>
        public static int FrontFacing(float[] bearings)
        {
            int best = 0;
            float bestError = float.MaxValue;
            for (int i = 0; i < bearings.Length; i++)
            {
                float error = Mathf.Abs(Mathf.DeltaAngle(bearings[i], -90f));
                if (error < bestError) { bestError = error; best = i; }
            }
            return best;
        }
    }
}
#endif
