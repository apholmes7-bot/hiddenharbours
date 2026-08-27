using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A rig-extracted 3D hull, as a committed asset (ADR 0022 phase 4).</b> Everything the facet
    /// render path needs to draw one hull mesh, in plain engine types: the mesh itself (a sub-asset),
    /// the rig's palette ramps and lighting constants, its dither matrix, its cell geometry — and the
    /// two per-artwork facts gameplay needs to POSE it (the measured azimuth convention and the rig's
    /// own rock amplitudes).
    ///
    /// <para><b>Builder-generated and committed</b>, like the <c>BoatVisualDef</c>s: the owner does
    /// not run anything to get a mesh hull, and re-runs the baker
    /// (Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake…) only when the art director's rig changes. Phase 3
    /// deliberately did not invent this format ("phase 4 owns turning this into a baked asset") —
    /// this is that format.</para>
    ///
    /// <para><b>Why it lives in Core.</b> The Boats module poses the hull (heading, wave rock) and
    /// the Art module draws it (the facet URP pass), and neither may reference the other
    /// (CLAUDE.md rule 4). This asset is the data they share, so it lives in the module both are
    /// allowed to see — the same reasoning as <c>CharacterVisualDef</c>. It deliberately contains no
    /// URP type: Art converts it to its own runtime setup on install.</para>
    ///
    /// <para>⚠️ <b><see cref="AzimuthCounterClockwise"/> is MEASURED, never assumed.</b> The baker
    /// runs <c>RigAzimuthProbe</c> over the rig's own rendered pixels — the declared facing order has
    /// been wrong five times in this project, and every time because someone trusted a declaration
    /// (see <c>iso-art-baked-counter-clockwise</c>). This flag is the mesh path's whole heading
    /// mapping: get it wrong and she sails stern-first at E/W. The end-to-end acceptance test
    /// compares the mesh render against her baked sheet through this very field, so a flip goes red
    /// in pixels, not in a code review.</para>
    /// </summary>
    public class HullMeshDef : ScriptableObject
    {
        /// <summary>One rig material: a palette ramp plus its constant shade-index offset.</summary>
        [Serializable]
        public struct Ramp
        {
            [Tooltip("The palette ramp, dark to light, exactly as the rig's MATS entry holds it.")]
            public Color32[] Colors;
            [Tooltip("The rig material's constant ramp-index offset ('off'; the blk/dark aliases are negative).")]
            public int Offset;
        }

        /// <summary>
        /// <b>One walkable level of this hull, as her rig declared it</b> — the row that lets a cabin
        /// say which faces to cut without anybody re-deriving anything.
        ///
        /// <para><b>⚠️ Three vocabularies name the same room, and mixing them draws the wrong one.</b>
        /// The rig calls it <c>house</c> (<see cref="LevelId"/>); <c>BoatInteriorDef</c> calls it
        /// <c>house_sole</c> (<see cref="DeckId"/>); the interior SHEETS run their own row order,
        /// which is neither. That last mismatch already shipped once — on the tanker, def index 2 is
        /// <c>house_sole</c> and sheet row 2 is <c>below</c>, so walking into the wheelhouse drew the
        /// engine space, and only a test noticed. The cure is the same here as there: the map is
        /// BUILDER-COMPUTED DATA carried from the rig (its <c>geometry().levels[].deck</c> field IS
        /// the def's level id), never a <c>_sole</c>-suffix rule invented at runtime.</para>
        /// </summary>
        [Serializable]
        public struct LevelTag
        {
            [Tooltip("The rig's own level name — 'house', 'cuddy', 'below', 'bridge', 'main_deck'.")]
            public string LevelId;

            [Tooltip("The BoatInteriorDef level id this is the same room as — 'house_sole', " +
                     "'cuddy_sole'. THE JOIN KEY, published by the rig; may be empty when the rig " +
                     "declared a level the interior kit never measured.")]
            public string DeckId;

            [Tooltip("The int this level's faces carry in TexCoord1.x. The gate compares against it.")]
            public int Tag;

            [Tooltip("True when the rig declared a real ceiling. An OPEN level (a working deck, the " +
                     "lobster's cockpit) is declared open, and cutting one would be cutting the sky — " +
                     "so the gate refuses it. An absent field and an open sky must never look the same.")]
            public bool Enclosed;

            [Tooltip("Sole height above the keel bottom, hull-local metres. A raked sole publishes " +
                     "its honest minimum, exactly as the rig does.")]
            public float SoleZMeters;

            [Tooltip("The overhead's underside, hull-local metres. Only meaningful when Enclosed.")]
            public float CeilingZMeters;
        }

        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): hullmesh.snake_case.")]
        public string Id = "hullmesh.unnamed";

        [Tooltip("The rig source this was extracted from (repo-relative), for provenance and re-bakes.")]
        public string SourceRigPath = "";

        /// <summary>
        /// The JS expression these faces came from, when the rig generates more than one hull —
        /// e.g. <c>LobsterBoatVariantsIso.variantFaces({size:'offshore',…})</c>.
        ///
        /// <para><b>Empty means the rig's static <c>F</c> array</b>, which is every hull baked before
        /// generators existed. It stays empty on those defs, and reaches YAML only when an asset is
        /// re-written — so adding this field churned nothing.</para>
        ///
        /// <para>Without it, eighteen lobster boats out of one file all claim the same provenance:
        /// same <see cref="SourceRigPath"/>, same global, different boat. Mirrors
        /// <c>HullPropMeshDef.SourceFaceBuilder</c>, which has recorded the same fact for fittings
        /// since ADR 0022 phase 7.</para>
        /// </summary>
        [Tooltip("For a rig that generates several hulls: the variant expression this one came from. " +
                 "Empty = the rig's static F array (every single-hull rig).")]
        public string SourceFaceBuilder = "";

        [Header("Geometry (extracted by RigMeshExtractor, built by RigMeshBuilder)")]
        [Tooltip("The hull mesh, stored as a sub-asset of this def. RigMeshBuilder layout: flat " +
                 "per-face normals, UV0 = (materialId, faceBias b, depthBias db, 0).")]
        public Mesh Mesh;

        [Header("Shading (the rig's own pipeline, verbatim)")]
        [Tooltip("Palette ramp + offset per rig material, in the rig's MATS order (max 16 — the facet shader's _RampMeta).")]
        public Ramp[] Ramps = Array.Empty<Ramp>();

        [Tooltip("The rig's key light LN, normalised, in the rig's own right-handed frame. The Art side " +
                 "applies the reflection sign — this is handed over untouched.")]
        public Vector3 LightN = Vector3.forward;
        public float Gain = 1f;
        public float Bias = 0f;

        [Tooltip("The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();

        public Color32 Keyline = new Color32(0, 0, 0, 255);

        [Header("Cell (the rig's screen geometry)")]
        [Tooltip("Cell pivot in pixels from the cell's TOP-LEFT — the rig's screen origin.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;
        public int CellW, CellH;
        [Tooltip("The rig's bake elevation (degrees above the horizon; 40 for the boat rigs).")]
        public float ElevationDeg = 40f;

        [Header("Cutaway (the rig's own geometry() — read, never derived)")]
        [Tooltip("One row per WALKABLE level the rig published, joining her mesh's TexCoord1.x tag to " +
                 "the BoatInteriorDef level id it is the same room as. EMPTY on every hull baked " +
                 "before the cutaway kit — an empty table means 'this hull cannot be cut', which is " +
                 "the honest answer for a mesh with no tags in it.")]
        public LevelTag[] LevelTags = Array.Empty<LevelTag>();

        [Header("Pose facts (per-artwork; measured or read off the rig — never tuned)")]
        [Tooltip("MEASURED azimuth convention (RigAzimuthProbe over rendered pixels): true = the rig's " +
                 "dir argument turns the hull COUNTER-CLOCKWISE (dir d depicts compass heading −45°·d), " +
                 "so the compass→dir mapping negates. True of every boat rig measured so far. " +
                 "⚠️ Load-bearing: flipping it mirrors the hull's heading end to end.")]
        public bool AzimuthCounterClockwise = true;

        [Tooltip("The rig's ROCK.rollA — peak roll (degrees) of its canned rock cycle. 0 = no rock.")]
        public float RockRollDegrees;
        [Tooltip("The rig's ROCK.pitchA — peak pitch (degrees).")]
        public float RockPitchDegrees;
        [Tooltip("The rig's ROCK.heaveA — peak heave (rig PIXELS; world metres = px / PxPerMetre).")]
        public float RockHeavePixels;

        [Header("Flotation (GAME-SIDE — the baker never writes this field, so it survives re-bakes)")]
        [Tooltip("THE DESIGN WATERLINE, in METRES above the rig origin — how far up this hull's " +
                 "planking the sea stands when she is floating at rest. Every boat rig's pivot is " +
                 "the KEEL BOTTOM ('amidships, keel bottom, centreline'), so this doubles as her " +
                 "resting draft, and 0 leaves a resting hull keel-ON-the-surface: troughs open air " +
                 "under the whole boat and only a crest ever wets the planking.\n\n" +
                 "THIS NUMBER IS NOW DRAWN EXACTLY (owner playtest 2026-08-07: 'generally they " +
                 "should level out at the boats water line'). It used to be subtracted from the " +
                 "hull's ride RAW, and the shared z-buffer's iso projection then drew the water " +
                 "climbing a PROJECTION GAIN of planking per metre of sink (0.9056 at the fleet's " +
                 "40 degree bake since ADR 0033 re-derived it; 1.1457 before) — so every hull in " +
                 "the fleet floated at a multiple of what this field claimed, always. " +
                 "HullSettleMath now pre-divides by that gain (derived from ElevationDeg below), " +
                 "so what is typed here " +
                 "is what the sea draws. Raise it to float her deeper, lower it for more freeboard " +
                 "— one number, one hull, no code.\n\n" +
                 "Applied ONLY while the displaced sea is active — with the flat water the render " +
                 "stays byte-identical to before ADR 0023 phase 3 (the A/B contract). Reference: " +
                 "the 3d-water spike framed the lobster boat at 0.5 m (spike/3d-water " +
                 "Spike3dWaterMenu.cs, 'sunk half a metre of draft'). Set BY HAND; the natural " +
                 "long-term home is the rig's gameplay sidecar (a WATERLINE symbol in the export " +
                 "contract) — migrate this field there when the art-director's export grows one, " +
                 "and have the baker write it.")]
        [Min(0f)] public float RestingDraftMeters = 0f;

        [Tooltip("The WATERTIGHT clamp (owner playtest 2026-07-23: 'water enters hull on the mesh " +
                 "models'): height above the KEEL (rig z = 0, metres) of the lowest OPEN interior " +
                 "surface — cockpit sole / hold floor / working deck. While the displaced sea is " +
                 "active the renderer bounds the calibrated waterline so the local water on the " +
                 "hull can never climb past this line: the waterline still rides the EXTERIOR " +
                 "planking, but never boards the boat. 0 = clamp off (the pre-fix render, " +
                 "byte-identical). GAME-SIDE like RestingDraftMeters — the baker never writes it, " +
                 "so it survives re-bakes. The rig source's own deck constant (the rig's DECK); " +
                 "the storm acceptance adjudicates it in pixels. Lower = drier = safer.")]
        [Min(0f)] public float WatertightDeckHeightMeters = 0f;

        [Tooltip("The watertight clamp's HALF-BEAM (rig ground metres): how far the hull's " +
                 "ground lines reach abeam of the root. Load-bearing for the far-rail beam " +
                 "residual — a water sample fights a LOWER height on each farther ground line " +
                 "(r − tan(elev)·ry), so the clamp must protect the worst line within this reach " +
                 "(docs/design/water-rendering.md §24). GAME-SIDE like the deck height (the " +
                 "baker never writes it). Slightly generous is safe (a touch drier); too small " +
                 "re-opens far-rail flooding — the storm acceptance adjudicates.")]
        [Min(0f)] public float WatertightHalfBeamMeters = 0f;

        /// <summary>
        /// True when this def can actually be drawn: a mesh, at least one non-empty ramp (≤ the
        /// shader's 16), a full 4×4 dither matrix and sane cell geometry. The skinner gates the mesh
        /// path on this and falls back to the sprite compass when it fails — an incomplete def must
        /// degrade to the shipped look, never to an invisible boat.
        /// </summary>
        public bool IsUsable()
        {
            if (Mesh == null) return false;
            if (Ramps == null || Ramps.Length == 0 || Ramps.Length > 16) return false;
            for (int i = 0; i < Ramps.Length; i++)
                if (Ramps[i].Colors == null || Ramps[i].Colors.Length == 0) return false;
            if (Bayer16 == null || Bayer16.Length != 16) return false;
            return PxPerMetre > 0 && CellW > 0 && CellH > 0;
        }

        /// <summary>
        /// <b>Can this hull be cut open?</b> True only when her rig published a level vocabulary AND
        /// her mesh was baked since — the two are separate facts and both have to hold, because a def
        /// re-serialised from a newer rig with an older mesh sub-asset is exactly the stale-bake state
        /// this repo keeps meeting.
        /// </summary>
        public bool CarriesLevelTags =>
            LevelTags != null && LevelTags.Length > 0 &&
            Mesh != null && Mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1);

        /// <summary>
        /// The TexCoord1.x tag for the <c>BoatInteriorDef</c> level id <paramref name="deckId"/>, or
        /// <b>0</b> — <c>hull</c>, the level that is never cut — when this hull has no such row, when
        /// the row exists but the rig declared that level OPEN, or when the id is empty.
        ///
        /// <para><b>0 is the refusal, and it is the same value as "gate off".</b> Cutting a level with
        /// no ceiling is cutting the sky; cutting a level this hull never declared is guessing. Both
        /// answer "draw her exterior", which is the shipped picture — a cutaway that does not happen
        /// is a missing feature, and one that happens to the wrong room is a broken boat.</para>
        ///
        /// <para>A linear scan over 2–4 rows. Called on a cabin transition, not per frame.</para>
        /// </summary>
        public int CutawayTagForDeck(string deckId)
        {
            if (string.IsNullOrEmpty(deckId) || LevelTags == null) return 0;
            for (int i = 0; i < LevelTags.Length; i++)
            {
                if (!string.Equals(LevelTags[i].DeckId, deckId, StringComparison.Ordinal)) continue;
                return LevelTags[i].Enclosed ? LevelTags[i].Tag : 0;
            }
            return 0;
        }
    }
}
