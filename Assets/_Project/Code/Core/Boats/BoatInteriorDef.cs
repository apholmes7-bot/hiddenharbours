using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>How a cabin door actually opens — the sidecars' <c>THRESHOLD.mechanism</c>.</summary>
    public enum BoatInteriorDoorMechanism
    {
        /// <summary>A sliding leaf on a track. No swept arc: the collider is the leaf itself, riding
        /// <c>doorOpen * travel</c> along its axis. Lobster, cape, both sport fishers, all 18 variants.</summary>
        Sliding = 0,
        /// <summary>A hinged leaf with a swept arc to keep clear. Dragger, both trawlers, packet, tanker.</summary>
        Hinged = 1,
    }

    /// <summary>
    /// Something standing on a cabin sole that the player cannot walk through — the sidecars'
    /// per-level <c>obstructions</c>.
    ///
    /// <para><b>Carried, not subtracted.</b> The level's <see cref="BoatInteriorLevel.Outline"/> is the
    /// whole sole with no holes cut in it, exactly as the deck sidecars do it: obstructions are listed
    /// beside the polygon and become game-side colliders. That treatment is the owner's 2026-07-22
    /// ruling (`docs/art/rigs/gameplay/README.md`), and it is why a bunk can be waist-high — something
    /// you see over — rather than a hole in the floor.</para>
    /// </summary>
    [Serializable]
    public sealed class BoatInteriorObstruction
    {
        [Tooltip("The sidecar's own id — 'bunk', 'helm_console', 'helm_deck_riser'.")]
        public string Id = "";

        [Tooltip("Plan footprint in hull-local metres (+x starboard, +y bow), as the sidecar states it.")]
        public Vector2[] Footprint = Array.Empty<Vector2>();

        [Tooltip("How far this stands above its own level's sole (m).")]
        public float HeightAboveSoleMeters;

        [Tooltip("The sidecar's own word for what it does to a walker: 'wall' (blocks and hides), " +
                 "'waist_block' (blocks, seen over). Carried verbatim — the runtime rules on it, not this.")]
        public string Treatment = "";
    }

    /// <summary>
    /// One standable plane inside the hull — the sidecars' <c>WALKABLE</c>, one entry per level.
    ///
    /// <para><b>A level is a LAYER, not a place</b> (ADR 0036, carried onto boats by ADR 0038). The
    /// <see cref="SoleZMeters"/> is hull-local height above the keel, so <c>below_sole</c> sitting under
    /// the waterline is simply a smaller z and not a special case.</para>
    /// </summary>
    [Serializable]
    public sealed class BoatInteriorLevel
    {
        [Tooltip("The sidecar's level key — 'house_sole', 'below_sole', 'helm_deck', 'bridge_sole'.")]
        public string Id = "";

        [Tooltip("Height of this sole above the keel, hull-local metres.")]
        public float SoleZMeters;

        [Tooltip("The sole's plan outline in hull-local metres, imported verbatim. No holes: see " +
                 "BoatInteriorObstruction.")]
        public Vector2[] Outline = Array.Empty<Vector2>();

        [Tooltip("What stands on this sole. May be empty — an empty helm deck is data, not a gap.")]
        public BoatInteriorObstruction[] Obstructions = Array.Empty<BoatInteriorObstruction>();

        /// <summary>True when this level has enough vertices to bound anything.</summary>
        public bool IsUsable() => Outline != null && Outline.Length >= 3;
    }

    /// <summary>
    /// The way in — the sidecars' <c>THRESHOLD</c>, one per interior.
    ///
    /// <para><b>The door is a declaration, not an animation.</b> The kit bakes an 8-frame cue
    /// (<c>doorOpen = k/7</c>, ~70 ms a frame) played reversed on exit; what is carried here is the
    /// frame count and the timing, so whoever plays it does not re-derive them.</para>
    /// </summary>
    [Serializable]
    public sealed class BoatInteriorDoor
    {
        [Tooltip("The sidecar's threshold id — 'entry', 'sky_entry'.")]
        public string Id = "";

        [Tooltip("Which end of the hull it is on, as the sidecar says: 'aft', 'fwd'.")]
        public string Side = "";

        [Tooltip("Clear opening width (m) — the leaf, not the whole aperture, on a two-panel slider.")]
        public float ClearWidthMeters;

        [Tooltip("Clear opening height (m).")]
        public float ClearHeightMeters;

        [Tooltip("The sill, hull-local metres (+x starboard, +y bow, +z up). z is the level you walk in " +
                 "onto — the sport fishers' sill sits at MEZZANINE height, so you walk in level.")]
        public Vector3 ThresholdPoint = Vector3.zero;

        [Tooltip("Sliding or hinged. A slider has no hinge axis and the sidecars flag that in their note.")]
        public BoatInteriorDoorMechanism Mechanism = BoatInteriorDoorMechanism.Sliding;

        [Tooltip("The area to keep clear of the moving leaf (or its swept arc), hull-local metres.")]
        public Vector2[] KeepClear = Array.Empty<Vector2>();

        [Tooltip("How many baked frames the open cue has. 8 across this whole kit.")]
        public int CueFrames;

        [Tooltip("Suggested milliseconds per cue frame (~70 across this kit).")]
        public float CueMillisecondsPerFrame;

        [Tooltip("Whether the cue is played backwards to close. True across this kit.")]
        public bool CueReversedOnExit = true;
    }

    /// <summary>A thing you can use inside — the sidecars' <c>INTERACT</c>. Ids are append-only from
    /// the kit forward.</summary>
    [Serializable]
    public sealed class BoatInteriorAnchor
    {
        [Tooltip("The sidecar's id — 'helm', 'bunk', 'locker', 'stove'.")]
        public string Id = "";

        [Tooltip("The verb, as the sidecar names it: 'enter_helm', 'sleep', 'storage', 'cook'.")]
        public string Action = "";

        [Tooltip("Where the player stands to reach it, hull-local metres.")]
        public Vector3 ReachPoint = Vector3.zero;

        [Tooltip("The facings this reads from — the kit's own 8-facing names. Empty means every facing.")]
        public string[] VisibleFacings = Array.Empty<string>();
    }

    /// <summary>
    /// A way between two levels — the sidecars' <c>STAIRS.companionways</c> and their <c>LADDER</c>
    /// legs, carried in one shape because a consumer only needs to know where it starts, where it ends
    /// and which mechanism plays.
    ///
    /// <para><see cref="ToLevel"/> may name a level this def does NOT own: the 53's companionway lands
    /// on <c>bridge_sole</c>, which is an EXTERIOR deck belonging to the hull's own gameplay sidecar.
    /// That is legal and deliberate — an interior stair reaching an outdoor deck is how the flybridge
    /// becomes reachable without a teleport — so the builder resolves route ends against the union of
    /// this def's levels and the hull's DECK ids, never against this def alone.</para>
    /// </summary>
    [Serializable]
    public sealed class BoatInteriorRoute
    {
        [Tooltip("The sidecar's id — 'house_to_below', 'lounge_to_helm_deck', 'flybridge_ladder'.")]
        public string Id = "";

        [Tooltip("Level id this route leaves from.")]
        public string FromLevel = "";

        [Tooltip("Level id it arrives at. MAY be an exterior DECK id — see the class doc.")]
        public string ToLevel = "";

        [Tooltip("What plays it: 'InteriorStair' for a companionway, 'vertical_ladder' for a ladder leg.")]
        public string Mechanism = "";

        [Tooltip("True for a leg that runs up the OUTSIDE of the house — the kit bakes and declares those.")]
        public bool Exterior;

        [Tooltip("Total climb (m), as the sidecar states it.")]
        public float TotalRiseMeters;
    }

    /// <summary>
    /// <b>The inside of one hull, as data (ADR 0003, rule 2; the architecture in ADR 0038).</b> One of
    /// these per hull instance, IMPORTED from that hull's
    /// <c>docs/art/rigs/boat-interiors-kit/&lt;stem&gt;.interior.json</c> by
    /// <c>BoatInteriorDefBuilder</c>. Nothing draws a cabin yet — this is the data half only.
    ///
    /// <para><b>Do not author this asset by hand.</b> Same rule and same reason as
    /// <c>BoatDeckDef</c>: the extractor carries the numbers straight through with no human copy step,
    /// because a hand-transcribed constant drifting from the rig is exactly how the baked-anchors JSON
    /// became dead code. The builder overwrites every field below on each run.</para>
    ///
    /// <para><b>Two pixel grids live in this kit, and conflating them is the trap.</b> The tanker bakes
    /// at 16 px/m; the rest of the fleet is 32. <see cref="PixelsPerMetre"/> is therefore stated per def,
    /// as its sidecar states it, and the builder REFUSES a sidecar that omits it. Anything compositing
    /// these sheets must read this field rather than assume the fleet default. The METRES never change —
    /// only how many pixels one of them is.</para>
    ///
    /// <para><b>Absence is data, and refusal is louder than absence.</b> A hull with no interior def has
    /// no cabin to enter — she has not been measured, and that is not an error. A hull whose sidecar was
    /// REFUSED at intake (`docs/art/rigs/boat-interiors-intake/s0-verdicts.json`) also has no def, and
    /// that IS an error someone must fix upstream: the builder names it on every run rather than
    /// quietly skipping it.</para>
    ///
    /// <para><b>Why Core.</b> Boats poses the hull and Art draws her, and neither may reference the
    /// other (rule 4) — the same reason <c>HullMeshDef</c> lives here.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Interior", fileName = "BoatInterior")]
    public sealed class BoatInteriorDef : ScriptableObject
    {
        [Header("Identity (imported — see the class doc before editing)")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): interior.<hull_def_id>, e.g. " +
                 "interior.sport_fisher_convertible_iso — the same hull name deck.<…> uses.")]
        public string Id = "";

        [Tooltip("The hull stems this interior fits, as the sidecar's fits_hulls states them.")]
        public string[] FitsHulls = Array.Empty<string>();

        [Header("Provenance (imported)")]
        [Tooltip("The interior sidecar these rooms were read from, repo-relative.")]
        public string SourceSidecar = "";
        [Tooltip("The interior RENDERER the sidecar was generated by, repo-relative.")]
        public string SourceInteriorRig = "";
        [Tooltip("The EXTERIOR hull rig the rooms were measured against, repo-relative.")]
        public string SourceHullRig = "";
        [Tooltip("The sidecar's derivedFromRigSha256 — the interior renderer's SHA — at import time. " +
                 "The builder refuses a sidecar whose renderer no longer hashes to this.")]
        public string InteriorRigSha256 = "";
        [Tooltip("The sidecar's hullRigSha256 — the exterior loft's SHA — at import time.")]
        public string HullRigSha256 = "";

        [Header("Cell (imported — ⚠️ px/m is PER HULL, see the class doc)")]
        [Tooltip("Pixels per metre this interior bakes at. 32 across the fleet; 16 on the tanker.")]
        public int PixelsPerMetre = 32;
        [Tooltip("The sheet cell in pixels (w, h) — the FULL hull cell, so interior composites under " +
                 "the exterior 1:1.")]
        public Vector2Int CellPixels = Vector2Int.zero;
        [Tooltip("The hull pivot within that cell, pixels.")]
        public Vector2Int PivotPixels = Vector2Int.zero;

        [Header("Geometry (imported)")]
        [Tooltip("The house envelope in hull-local metres — the sidecar's FOOTPRINT. ONE footprint per " +
                 "hull, shared by every level, which is what makes true-to-footprint structurally " +
                 "unable to fail (ADR 0036's property, kept).")]
        public Vector2[] FootprintOutline = Array.Empty<Vector2>();

        [Tooltip("Every standable plane inside her, deepest first is NOT guaranteed — read SoleZMeters.")]
        public BoatInteriorLevel[] Levels = Array.Empty<BoatInteriorLevel>();

        [Tooltip("The way in.")]
        public BoatInteriorDoor Door = new BoatInteriorDoor();

        [Tooltip("Additional doors beyond the main threshold — the 90's skylounge slider onto the aft " +
                 "deck. Empty on every hull with one way in.")]
        public BoatInteriorDoor[] AdditionalDoors = Array.Empty<BoatInteriorDoor>();

        [Tooltip("What you can use inside. Ids append-only from the kit forward.")]
        public BoatInteriorAnchor[] Anchors = Array.Empty<BoatInteriorAnchor>();

        [Tooltip("Companionways and ladder legs, interior and exterior alike.")]
        public BoatInteriorRoute[] Routes = Array.Empty<BoatInteriorRoute>();

        [Header("Motion (imported — ADR 0038 proposal 1)")]
        [Tooltip("Whether these sheets ride the hull's rock(i) on the same camera basis. True across " +
                 "this kit: pass the SAME roll/pitch/heave to both renders and registration holds " +
                 "mid-wave. The comfort clamp scales what the INTERIOR is posed with; it never touches " +
                 "the hull, and it is not stored here because it is a player setting, not hull data.")]
        public bool RidesHullRock = true;

        /// <summary>The level with this id, or null. Linear over a handful of levels — no hull in the
        /// kit has more than four.</summary>
        public BoatInteriorLevel Level(string levelId)
        {
            if (Levels == null || string.IsNullOrEmpty(levelId)) return null;
            for (int i = 0; i < Levels.Length; i++)
                if (Levels[i] != null && string.Equals(Levels[i].Id, levelId, StringComparison.Ordinal))
                    return Levels[i];
            return null;
        }

        /// <summary>True when at least one level is usable — the gate a consumer takes the interior
        /// path behind. False means she has no inside to enter, and that is data.</summary>
        public bool HasInterior()
        {
            if (Levels == null) return false;
            for (int i = 0; i < Levels.Length; i++)
                if (Levels[i] != null && Levels[i].IsUsable()) return true;
            return false;
        }

        /// <summary>How many metres one baked pixel is on THIS hull — the tanker's 1/16 against the
        /// fleet's 1/32. Zero px/m would be a builder bug; it reports rather than dividing by it.</summary>
        public float MetresPerPixel => PixelsPerMetre > 0 ? 1f / PixelsPerMetre : 0f;
    }
}
