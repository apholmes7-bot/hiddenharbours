using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Where a baked interior actually IS, in world metres — the pure geometry every part of the pilot
    /// reads: what counts as inside, where the walls stand, and where the threshold is.
    ///
    /// <para>Pure and struct-shaped on purpose: no scene, no assets, no <c>MonoBehaviour</c>, so the
    /// arithmetic is unit-tested headless. It has to be, because every one of its failure modes is
    /// SILENT — a room whose walls are in the wrong place still draws perfectly and simply stops the
    /// player somewhere that looks like nothing.</para>
    ///
    /// <para><b>⚠️ THE WORLD XY PLANE IS THE SQUASHED GROUND PLANE.</b> The shared ¾ bake camera sits
    /// 40° above the horizon, so one metre of NORTHWARD ground travel draws
    /// <c>sin 40° ≈ 0.643</c> world units up the screen while one metre EASTWARD draws a full unit
    /// across (<c>SpriteLightMath.GroundDepthScale</c> is the definition; it is passed in as
    /// <see cref="DepthScale"/> rather than restated here because the World module does not reference
    /// Art). A room that is 6.6 × 8.05 m of floor therefore occupies 6.6 × 5.2 world units, and a
    /// footprint box built without that squash is half again too deep — the player would be stopped
    /// three metres past the drawn back wall.</para>
    ///
    /// <para><b>⚠️ And a rotated footprint is a PARALLELOGRAM, not a rectangle.</b> Squashing one axis
    /// after rotating shears the box, which is why the walls come out of <see cref="WallQuads"/> as
    /// explicit quads for a <see cref="PolygonCollider2D"/> and not as rotated
    /// <see cref="BoxCollider2D"/>s. A box collider cannot express a shear, and at the four diagonal
    /// facings — half of them — it would be visibly wrong.</para>
    ///
    /// <para><b>The model frame is the ROOM's</b>, matching what the owner sees in the room art:
    /// <c>+x</c> is across the room to the right and <c>+y</c> is toward the far wall. For the HOUSE
    /// family the doorway is at <c>(0, −Ln/2)</c> — <c>interiorIsoRig</c>'s own frame, whose door is on
    /// <c>−Y</c> while its exterior shell's is on <c>+Y</c>, which is exactly why the two sheets are
    /// shown <c>InteriorKit.ExteriorFacingOffset</c> facings apart.</para>
    ///
    /// <para><b>⚠️ THE DOOR IS NOT ALWAYS ON −Y, AND IT IS NOT ALWAYS CENTRED.</b> The SHOP kit's room
    /// is its shopfront seen from inside, so its street door is on <c>+Y</c> — the opposite wall — and
    /// on two of its three trades it sits well off centre (the post office's is 1.68 m left of the wall
    /// centre, the restaurant's 2.52 m). Both facts are MEASURED from the bake's own per-facing door
    /// anchors, not read off a README. <see cref="DoorSign"/> and <see cref="DoorAcrossMetres"/> carry
    /// them, and both default to the house's answer so nothing that already stands moves.</para>
    ///
    /// <para>Getting either wrong is silent in the worst way: the room draws perfectly, and the gap the
    /// player can walk through is somewhere other than the doorway they can see.</para>
    /// </summary>
    public readonly struct InteriorFootprint
    {
        /// <summary>The room's floor CENTRE in world units — the same point the room sprite's pivot
        /// sits on, and the same point its exterior shell stands at.</summary>
        public readonly Vector2 Centre;

        /// <summary>Floor width (across, the rig's <c>Wd</c>) and length (front to back, <c>Ln</c>) in
        /// GROUND metres — unsquashed, as the rig reports them.</summary>
        public readonly float WidthMetres, LengthMetres;

        /// <summary>Which interior facing is being shown, and out of how many. The facing IS the
        /// rotation: the art is baked at a fixed camera, so nothing ever rotates a transform.</summary>
        public readonly int Facing, Facings;

        /// <summary>How far up the screen one metre of northward ground travel draws —
        /// <c>sin(camera elevation)</c>, 0.643 at the shared 40°.</summary>
        public readonly float DepthScale;

        /// <summary>Which model-frame wall the doorway is in: <c>−1</c> for the <c>−y</c> wall (the
        /// house family, and the default) or <c>+1</c> for <c>+y</c> (the shop family). Never 0 — a
        /// building with no door is not a building this struct can describe.</summary>
        public readonly float DoorSign;

        /// <summary>How far along that wall the doorway sits, in metres from the wall's centre,
        /// <c>+x</c> to the right. 0 for a centred door, which is every house.</summary>
        public readonly float DoorAcrossMetres;

        public InteriorFootprint(Vector2 centre, float widthMetres, float lengthMetres,
                                 int facing, int facings, float depthScale,
                                 float doorSign = -1f, float doorAcrossMetres = 0f)
        {
            Centre = centre;
            WidthMetres = widthMetres;
            LengthMetres = lengthMetres;
            Facing = facing;
            Facings = Mathf.Max(1, facings);
            DepthScale = depthScale;
            DoorSign = doorSign >= 0f ? 1f : -1f;

            // Clamped so a mis-measured anchor cannot push the doorway off the end of its own wall and
            // leave the room sealed — the one failure here that would look like a physics bug.
            float halfWidth = Mathf.Max(0f, widthMetres * 0.5f);
            DoorAcrossMetres = Mathf.Clamp(doorAcrossMetres, -halfWidth, halfWidth);
        }

        /// <summary>
        /// The building's yaw on the GROUND, in degrees, for the facing it is showing.
        ///
        /// <para>Negative because of how the rigs turn: cell <c>i</c> renders the model rotated by
        /// <c>−45°·i</c> under a fixed camera (<c>RigBaker.DirForCell</c> applies the measured
        /// counter-clockwise correction, and the rigs then compute <c>th = dir·45°</c>), so walking
        /// through the cells turns the building clockwise on the ground.</para>
        /// </summary>
        public float YawDegrees => -360f * Facing / Facings;

        /// <summary>A model-frame point (metres) in world units. Rotate on the GROUND first, squash
        /// second — the other order shears the wrong way and is not distinguishable by eye at the
        /// orthogonal facings, only at the diagonals.</summary>
        public Vector2 ModelToWorld(Vector2 model)
        {
            float rad = YawDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            float gx = model.x * c - model.y * s;
            float gy = model.x * s + model.y * c;
            return new Vector2(Centre.x + gx, Centre.y + gy * DepthScale);
        }

        /// <summary>A world point back in the model frame (metres). Un-squash first, then un-rotate.</summary>
        public Vector2 WorldToModel(Vector2 world)
        {
            float gx = world.x - Centre.x;
            float gy = Mathf.Approximately(DepthScale, 0f)
                ? 0f
                : (world.y - Centre.y) / DepthScale;

            float rad = -YawDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(gx * c - gy * s, gx * s + gy * c);
        }

        /// <summary>
        /// Is this world point standing on the room's floor?
        ///
        /// <para><paramref name="inset"/> shrinks the test rectangle by that many metres on every side.
        /// Pass the wall thickness and "inside" means PAST the wall rather than in it — which is what
        /// makes crossing the threshold, and only crossing the threshold, an entry.</para>
        /// </summary>
        public bool Contains(Vector2 world, float inset)
        {
            Vector2 m = WorldToModel(world);
            float hw = WidthMetres * 0.5f - inset;
            float hl = LengthMetres * 0.5f - inset;
            if (hw <= 0f || hl <= 0f) return false;
            return Mathf.Abs(m.x) <= hw && Mathf.Abs(m.y) <= hl;
        }

        /// <summary>The doorway in world units — the threshold the player crosses, and the point the
        /// bake's own door anchor lands on. Reads <see cref="DoorSign"/> for which wall and
        /// <see cref="DoorAcrossMetres"/> for where along it, so it is the DRAWN door and not the
        /// centre of a wall that happens to have one.</summary>
        public Vector2 DoorWorld =>
            ModelToWorld(new Vector2(DoorAcrossMetres, DoorSign * LengthMetres * 0.5f));

        /// <summary>The room's four corners in world units, front-left first, going round. A
        /// parallelogram at every facing except the four orthogonal ones.</summary>
        public Vector2[] Corners()
        {
            float hw = WidthMetres * 0.5f, hl = LengthMetres * 0.5f;
            return new[]
            {
                ModelToWorld(new Vector2(-hw, -hl)),
                ModelToWorld(new Vector2(hw, -hl)),
                ModelToWorld(new Vector2(hw, hl)),
                ModelToWorld(new Vector2(-hw, hl)),
            };
        }

        /// <summary>
        /// The walls, as world-space quads for <see cref="PolygonCollider2D"/>: back, left, right, and
        /// the front wall split in two around the doorway. Five quads, always in that order.
        ///
        /// <para><b>Every wall blocks, including the two the bake DROPPED.</b> The open-dollhouse
        /// cutaway is a courtesy to the camera, not a hole in the house — a player who could walk out
        /// through the missing near wall would be walking through a wall that exists.</para>
        ///
        /// <para><paramref name="doorwayWidth"/> is deliberately allowed to be wider than the drawn
        /// opening: the gap has to admit a player who has width of their own, and a threshold you have
        /// to line up on to the pixel is not cozy.</para>
        ///
        /// <para>The side walls are trimmed to sit BETWEEN the front and back ones rather than running
        /// the full length. The quads are then disjoint, which matters: several paths on one
        /// <see cref="PolygonCollider2D"/> turn their overlaps into HOLES, so four walls that shared
        /// their corners would leave a gap at every corner of the house — and the only symptom would be
        /// a player who occasionally slips through a corner.</para>
        /// </summary>
        public Vector2[][] WallQuads(float thickness, float doorwayWidth)
        {
            float hw = WidthMetres * 0.5f, hl = LengthMetres * 0.5f;
            float t = Mathf.Clamp(thickness, 0.01f, Mathf.Min(hw, hl));
            float half = Mathf.Clamp(doorwayWidth, 0f, WidthMetres) * 0.5f;

            // The gap goes where the DOOR is, not at the middle of the wall — and it is kept inside the
            // wall's own ends, so an off-centre door near a corner narrows its opening rather than
            // opening the corner itself.
            float gap0 = Mathf.Clamp(DoorAcrossMetres - half, -hw, hw);
            float gap1 = Mathf.Clamp(DoorAcrossMetres + half, -hw, hw);

            // The doorway wall is at DoorSign·hl; the solid one opposite it.
            float doorOuter = DoorSign * hl;
            float doorInner = DoorSign * (hl - t);
            float backOuter = -doorOuter;
            float backInner = -doorInner;

            return new[]
            {
                Quad(-hw, hw, Mathf.Min(backInner, backOuter), Mathf.Max(backInner, backOuter)),
                Quad(-hw, -hw + t, -hl + t, hl - t),  // LEFT  (−x), between the other two
                Quad(hw - t, hw, -hl + t, hl - t),    // RIGHT (+x), between the other two
                Quad(-hw, gap0, Mathf.Min(doorInner, doorOuter), Mathf.Max(doorInner, doorOuter)),
                Quad(gap1, hw, Mathf.Min(doorInner, doorOuter), Mathf.Max(doorInner, doorOuter)),
            };
        }

        /// <summary>
        /// <b>The doorway gap, as a solid quad</b> — exactly the piece <see cref="WallQuads"/> leaves
        /// out, so a storey with no front door of its own can close it.
        ///
        /// <para>An upper storey shares this footprint and therefore shares its walls, but it does NOT
        /// share the door: there is nothing outside a first-floor doorway but air. Rather than build a
        /// second, subtly different wall set for the upper level, the upper level adds this ONE quad and
        /// the house is solid. Deriving it here — from the same <c>gap0</c>/<c>gap1</c> arithmetic that
        /// cuts the gap — is the point: a plug computed independently would be a second place for the
        /// door's position to live, and the two would drift the first time a rig moved a door off
        /// centre.</para>
        /// </summary>
        public Vector2[] DoorwayPlugQuad(float thickness, float doorwayWidth)
        {
            float hw = WidthMetres * 0.5f, hl = LengthMetres * 0.5f;
            float t = Mathf.Clamp(thickness, 0.01f, Mathf.Min(hw, hl));
            float half = Mathf.Clamp(doorwayWidth, 0f, WidthMetres) * 0.5f;

            float gap0 = Mathf.Clamp(DoorAcrossMetres - half, -hw, hw);
            float gap1 = Mathf.Clamp(DoorAcrossMetres + half, -hw, hw);

            float doorOuter = DoorSign * hl;
            float doorInner = DoorSign * (hl - t);

            return Quad(gap0, gap1, Mathf.Min(doorInner, doorOuter), Mathf.Max(doorInner, doorOuter));
        }

        /// <summary>A model-frame axis-aligned rectangle as a world-space quad (sheared by the
        /// squash at any non-orthogonal facing).</summary>
        public Vector2[] Quad(float x0, float x1, float y0, float y1) => new[]
        {
            ModelToWorld(new Vector2(x0, y0)),
            ModelToWorld(new Vector2(x1, y0)),
            ModelToWorld(new Vector2(x1, y1)),
            ModelToWorld(new Vector2(x0, y1)),
        };

        /// <summary>
        /// A free-standing prop's footprint as a world quad: a <paramref name="width"/> ×
        /// <paramref name="depth"/> ground rectangle, centred on a model-frame point and turned by
        /// <paramref name="propFacingOffset"/> facings relative to the room.
        ///
        /// <para>The prop rig's origin is the floor-CENTRE of the prop's own footprint — the same
        /// convention as the room's pivot — which is the whole reason a prop drops onto a room floor
        /// point with no offset maths.</para>
        /// </summary>
        public Vector2[] PropQuad(Vector2 modelCentre, float width, float depth, int propFacingOffset)
        {
            var turned = new InteriorFootprint(ModelToWorld(modelCentre), width, depth,
                                               ((Facing + propFacingOffset) % Facings + Facings) % Facings,
                                               Facings, DepthScale);
            return turned.Corners();
        }
    }
}
