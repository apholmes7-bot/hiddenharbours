using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Which of the carrier's hands a prop is in — already resolved for the heading, so a
    /// consumer never re-derives the swap.</summary>
    public enum CarryHandSide
    {
        /// <summary>No hand at all — a <see cref="CarryMountKind.Back"/> mount.</summary>
        None = 0,
        Right = 1,
        Left = 2,
        /// <summary>Both, held at the wrists' midpoint — the rig's two-handed <c>cradle</c>.</summary>
        Both = 3,
    }

    /// <summary>Where on the body a prop rides.</summary>
    public enum CarryMountKind
    {
        /// <summary>In a hand (or across both): the anchor is a WRIST and moves every frame.</summary>
        Hand = 0,
        /// <summary>Slung on the back: the anchor is a point up the torso, not a wrist.</summary>
        Back = 1,
    }

    /// <summary>
    /// One prop at one facing — the whole of how a carried thing is held at that heading.
    ///
    /// <para><b>The two point fields are different quantities and the names say so</b> (the sidecar's own
    /// warning, kept). <see cref="GripOffsetMeters"/> is the offset FROM THE ANCHOR and is
    /// frame-independent: add it to the live wrist and the prop tracks a swinging hand.
    /// <see cref="RestOffsetMeters"/> is absolute, from the character's pivot, at the REST POSE ONLY.
    /// Reading the rest pin as if it were live is the one mistake this table can invite — the fisher's
    /// right wrist swings 8.3 px across a walk cycle and 11.5 px across a run, so a prop pinned at rest
    /// would visibly come off her hand the moment she moved.</para>
    /// </summary>
    [Serializable]
    public struct CarryAnchorRow
    {
        [Tooltip("Which hand holds it AT THIS FACING — already swapped by the rig. A consumer never " +
                 "re-derives this: the small props move to the near hand on the away headings so they " +
                 "do not sit behind the torso, and which headings those are is measured art, not a rule.")]
        public CarryHandSide Hand;

        [Tooltip("Wrist mount or back sling.")]
        public CarryMountKind Mount;

        [Tooltip("Offset from the ANCHOR (the wrist, or the sling point), world metres, frame-independent.")]
        public Vector2 GripOffsetMeters;

        [Tooltip("Absolute pin from the character's PIVOT, world metres, at the rest pose named on the " +
                 "table — NOT a live position. The fallback when no live anchor is available.")]
        public Vector2 RestOffsetMeters;

        [Tooltip("Which cell of the PROP'S OWN turntable to draw. Not the carrier's facing row: a fish " +
                 "is turned across the view at N and S, where a body-aligned fish is end-on and reads " +
                 "as a blob.")]
        public int ItemFacing;

        [Tooltip("Draw UNDER the carrier's sprite at this facing. ⚠️ Not a depth test — the rig requires " +
                 "the prop to be both deeper than the body axis AND laterally inside the torso, because " +
                 "depth alone is what used to make every small held item vanish at North.")]
        public bool Behind;
    }

    /// <summary>One prop's eight facing rows, plus the provenance of the art that draws it.</summary>
    [Serializable]
    public sealed class CarryPropAnchors
    {
        [Tooltip("The rig's own prop key — rodTrail / rodSling / fish / clam / knife / gaff / rope. " +
                 "What ICarryAnchored.HandPropKey matches on.")]
        public string PropKey;

        [Tooltip("Which prop rig draws it (RodIso / FishIso / Shellfish / RopeProps). EMPTY means the " +
                 "rig bakes no art for this row yet and it carries a spec only — knife and gaff are " +
                 "the declared art debts. A row with no rig still poses correctly; there is simply " +
                 "nothing to pose.")]
        public string Rig;

        [Tooltip("Scale the prop's own art is drawn at (the carried rope hank is a half-scale deck coil).")]
        public float ItemScale;

        [Tooltip("One row per facing, indexed by the carrier's facing ROW — dir 0 is the North picture.")]
        public CarryAnchorRow[] Rows = Array.Empty<CarryAnchorRow>();
    }

    /// <summary>
    /// The carrier's live WRIST positions for one gait: world metres from the character's pivot, per
    /// <c>[facingRow · FramesPerDir + frame]</c> — the same flattened layout every other anchor table in
    /// this project uses.
    /// </summary>
    [Serializable]
    public sealed class CarryWristFrames
    {
        [Tooltip("The gait these frames belong to. One entry per gait the body can actually be drawn in.")]
        public CharacterGait Gait;

        [Tooltip("Frames per direction on this gait's sheet — must match the visual def's own count, or " +
                 "the pin would index a frame the body is not drawing.")]
        public int FramesPerDir;

        public Vector2[] Left = Array.Empty<Vector2>();
        public Vector2[] Right = Array.Empty<Vector2>();
    }

    /// <summary>
    /// <b>Where a carried thing actually hangs</b> — the character rig's hand-prop table as an asset
    /// (rule 2 / ADR 0003), imported from the baked sidecar's <c>handProps</c> block and its per-frame
    /// <c>anchors</c> grid, converted once to world metres.
    ///
    /// <para><b>What this replaced.</b> One serialized <c>Vector2</c> on the hands, applied unchanged at
    /// every heading. That was honest when it was written — the fuel kit bakes no hand anchors, so there
    /// was nothing to read a per-facing grip from — and it is wrong now that there is: the clam's own pin
    /// swings 13.3 px in x (0.42 m at this kit's 32 px/m) across the eight facings, and the draw order
    /// the old rule derived from the heading disagrees with the rig at North, which is the heading where
    /// a small held item used to disappear behind the body.</para>
    ///
    /// <para><b>Why the table is the CARRIER's and not the carried thing's.</b> Every number here is a
    /// fact about the fisher's arms — where her wrists are at each heading and each frame, how wide her
    /// torso is. A clam does not know that. The carried object contributes exactly one thing, its
    /// <see cref="ICarryAnchored.HandPropKey"/>, and this answers the rest.</para>
    ///
    /// <para><b>Two anchors, one rule.</b> A hand-mounted prop is pinned LIVE — the wrist for this gait,
    /// facing and frame, plus the row's frame-independent grip offset. When no live wrist is available
    /// (a stance or clip this table did not import, or a carrier with no iso skin at all) the row's rest
    /// pin stands in: still per-facing, still from the rig, just not tracking the swing.
    /// <see cref="PinMeters"/> is the whole of that decision and it is one method deliberately, so a
    /// consumer cannot pick the wrong field.</para>
    ///
    /// <para><b>The relation that pins the import.</b> The sidecar's rest pin is, exactly,
    /// <c>anchor(rest anim, rest frame, dir) + grip</c> — verified for all 56 baked rows including the
    /// back mount. So evaluating the LIVE path at the rest pose must reproduce the imported rest pin,
    /// and a test asserts precisely that: any drift in the wrist grid, the metre conversion or the
    /// composition rule fires it.</para>
    ///
    /// <para><b>Two things this table deliberately does not carry.</b> The rig's yaw/pitch/bend per row
    /// (a 2D sprite prop has no such freedom — those are the spec for a future 3D-posed prop), and a
    /// live anchor for the <see cref="CarryMountKind.Back"/> sling: the sling point is a fraction of the
    /// way from hip to head that the sidecar does not publish, so tracking it live would mean restating
    /// a rig constant here. The back row imports its rest pin and nothing poses it yet.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CarryAnchorTable",
                     menuName = "Hidden Harbours/Art/Carry Anchor Table")]
    public sealed class CarryAnchorTableDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (carryanchors.fisher_iso).")]
        public string Id;

        [Tooltip("Provenance: the baked sidecar this was imported from. Re-run the importer after a " +
                 "character re-bake; never hand-edit the numbers below.")]
        public string SourceSidecar;

        [Tooltip("The rig pass that baked it (CharacterHands6.pass). Provenance only.")]
        public float Pass;

        [Tooltip("How many facing rows — the carrier's own FacingCount, 8 for every shipped character.")]
        [Min(1)] public int FacingCount = 8;

        [Tooltip("Half the torso's width in metres, from the rig. Provenance for the Behind rule, which " +
                 "is already resolved per row; nothing recomputes it.")]
        public float TorsoHalfMetres;

        [Tooltip("The pose the rest pins were measured at — provenance, so a reader never has to infer " +
                 "which frame 'rest' means.")]
        public string RestAnim;

        [Tooltip("The frame of RestAnim the rest pins were measured at.")]
        public int RestFrame;

        [Tooltip("One entry per prop the rig bakes a row for, in the rig's declared order.")]
        public CarryPropAnchors[] Props = Array.Empty<CarryPropAnchors>();

        [Tooltip("The live wrist grid, one entry per gait. A gait with no entry falls back to the rest pin.")]
        public CarryWristFrames[] Wrists = Array.Empty<CarryWristFrames>();

        // Lookup cache. Rebuilt on reload — the same arrangement CatchItemLibrary uses, and for the same
        // reason: the pose is stated every frame while something is carried (rule 7), so the per-frame
        // path must not allocate or scan.
        [NonSerialized] private Dictionary<string, CarryPropAnchors> _byKey;

        private void OnEnable() => _byKey = null;

        /// <summary>True when this table has at least one prop with at least one row — the null-safe
        /// "is there anything to read" check a consumer gates on before preferring it.</summary>
        public bool HasRows
        {
            get
            {
                if (Props == null) return false;
                foreach (CarryPropAnchors p in Props)
                    if (p != null && p.Rows != null && p.Rows.Length > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// The row for one prop at one facing. False — leaving <paramref name="row"/> default — for an
        /// empty key, an unknown prop, or a facing outside the baked rows, all of which are ordinary:
        /// a jerry can has no key, the shovel has no row, and the caller then keeps its own offset.
        /// </summary>
        public bool TryRow(string propKey, int facingRow, out CarryAnchorRow row)
        {
            row = default;
            if (string.IsNullOrEmpty(propKey) || Props == null) return false;

            _byKey ??= BuildLookup();
            if (!_byKey.TryGetValue(propKey, out CarryPropAnchors prop) || prop.Rows == null) return false;
            if (facingRow < 0 || facingRow >= prop.Rows.Length) return false;

            row = prop.Rows[facingRow];
            return true;
        }

        /// <summary>The whole entry for a prop (rig, scale, every row) — for tooling and tests. Null when
        /// the key is unknown.</summary>
        public CarryPropAnchors Prop(string propKey)
        {
            if (string.IsNullOrEmpty(propKey) || Props == null) return null;
            _byKey ??= BuildLookup();
            return _byKey.TryGetValue(propKey, out CarryPropAnchors prop) ? prop : null;
        }

        /// <summary>
        /// The live ANCHOR a hand-mounted row hangs from: the wrist (or the wrists' midpoint for a
        /// two-handed cradle) in world metres from the character's pivot. False when this gait was not
        /// imported, the indices are out of range, or the row is not hand-mounted — every one of which
        /// is a "use the rest pin" answer rather than an error.
        /// </summary>
        public bool TryAnchorMeters(CharacterGait gait, int facingRow, int frame, CarryHandSide hand,
                                    out Vector2 anchor)
        {
            anchor = default;
            if (Wrists == null || hand == CarryHandSide.None) return false;
            if (facingRow < 0 || facingRow >= Mathf.Max(1, FacingCount)) return false;

            foreach (CarryWristFrames w in Wrists)
            {
                if (w == null || w.Gait != gait || w.FramesPerDir <= 0) continue;

                // Wrap rather than refuse: the body's frame counter is the visual def's, and a def whose
                // sheet is one frame shorter than the bake would otherwise silently drop the prop back to
                // its rest pin for that one cell — a stutter far harder to see than a wrapped hand.
                int f = ((frame % w.FramesPerDir) + w.FramesPerDir) % w.FramesPerDir;
                int i = facingRow * w.FramesPerDir + f;

                bool wantsLeft = hand != CarryHandSide.Right;
                bool wantsRight = hand != CarryHandSide.Left;
                if (wantsLeft && (w.Left == null || i >= w.Left.Length)) return false;
                if (wantsRight && (w.Right == null || i >= w.Right.Length)) return false;

                anchor = hand switch
                {
                    CarryHandSide.Left => w.Left[i],
                    CarryHandSide.Right => w.Right[i],
                    _ => (w.Left[i] + w.Right[i]) * 0.5f,
                };
                return true;
            }
            return false;
        }

        /// <summary>
        /// <b>Where the prop hangs, in world metres from the carrier's pivot</b> — the one call a carrier
        /// makes, so it cannot choose the wrong anchor.
        ///
        /// <para>Live wrist + grip when this gait's wrists were imported and the row is hand-mounted;
        /// the row's rest pin otherwise. <paramref name="live"/> reports which answer came back, for a
        /// caller that wants to log or test the distinction — nothing in the runtime branches on it.</para>
        /// </summary>
        public Vector2 PinMeters(in CarryAnchorRow row, CharacterGait gait, int facingRow, int frame,
                                 out bool live)
        {
            if (row.Mount == CarryMountKind.Hand
                && TryAnchorMeters(gait, facingRow, frame, row.Hand, out Vector2 anchor))
            {
                live = true;
                return anchor + row.GripOffsetMeters;
            }

            live = false;
            return row.RestOffsetMeters;
        }

        /// <inheritdoc cref="PinMeters(in CarryAnchorRow, CharacterGait, int, int, out bool)"/>
        public Vector2 PinMeters(in CarryAnchorRow row, CharacterGait gait, int facingRow, int frame)
            => PinMeters(row, gait, facingRow, frame, out _);

        private Dictionary<string, CarryPropAnchors> BuildLookup()
        {
            var map = new Dictionary<string, CarryPropAnchors>(StringComparer.Ordinal);
            if (Props == null) return map;
            foreach (CarryPropAnchors p in Props)
                if (p != null && !string.IsNullOrEmpty(p.PropKey))
                    map[p.PropKey] = p;      // first-wins would hide a duplicate; last-wins matches the array
            return map;
        }
    }
}
