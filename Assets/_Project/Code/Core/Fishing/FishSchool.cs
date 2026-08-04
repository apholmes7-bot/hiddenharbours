using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ONE SCHOOL OF FISH — an <b>area</b> of water, at a <b>depth</b>, for a <b>while</b> (ADR 0025 S3).
    /// The single object the fish finder draws and the fishing path fishes.
    ///
    /// <para><b>The honesty invariant this type exists to enforce.</b> The owner's ruling is that whatever
    /// the glass shows must be the same object that changes the fishing: the finder READS a school to draw
    /// its marks; the bite path reads the SAME school to raise the bite rate and weight the species roll.
    /// Delete the finder tomorrow and the schools still exist and still fish the same. That is only true if
    /// there is exactly ONE record — this one — rather than a sonar picture and a hidden bite table that can
    /// drift apart. <see cref="MarkCount"/> is the whole invariant in one field: it is both how many fish
    /// the glass draws AND the expected bite rate.</para>
    ///
    /// <para><b>Metres, always.</b> <see cref="DepthMetres"/> and <see cref="RadiusMetres"/> are world
    /// metres, the one frame the tide model, <c>ITidalTerrain</c> and <c>DepthSounder.DisplayDepth</c> all
    /// use. The rig wants a NORMALISED 0..1 column position (<c>fishRig.js</c> — the rig turns
    /// <c>y·range</c> back into a depth), and the presenter computes that at paint time from the player's
    /// current range. <b>A normalised depth is never stored here</b>: it would silently mean something
    /// different the moment the RANGE pusher moved.</para>
    ///
    /// <para><b>Recomputed, never saved</b> (rule 5). Schools are a function of
    /// <c>(worldSeed, gameTime, place, weather)</c> exactly as the tide and the weather are, so nothing
    /// about them belongs in <c>SaveData</c> — there is deliberately no DTO for this type and no field on
    /// the save that names a school. What DOES persist is the player's finder <i>preference</i>
    /// (<c>SounderPrefs.RangeMetres</c>), which is a display scale, not a fish.</para>
    ///
    /// <para><b>Engine-light.</b> Only <see cref="Vector2"/>/<see cref="Mathf"/> — the same latitude
    /// <c>ITidalTerrain</c> takes. Every member below is a pure function of the struct's own fields, so the
    /// whole thing is EditMode-testable without a scene, a boat or a canvas.</para>
    /// FLAG lead-architect: new Core contract (the ADR 0025 S3 fish-school seam).
    /// </summary>
    public readonly struct FishSchool
    {
        /// <summary>Centre of the school's area, in world space.</summary>
        public readonly Vector2 Centre;

        /// <summary>How far from <see cref="Centre"/> the boat may be and still be ON the school, in
        /// metres. The school owns its own nearness test — there is no second search radius anywhere for
        /// it to get out of sync with. A radius ≤ 0 is an empty school (nothing is inside it).</summary>
        public readonly float RadiusMetres;

        /// <summary>Approximate depth of the fish below the surface, in METRES — where the marks sit in
        /// the water column, and the depth at which bites happen (the owner's ruling: "bites happen at
        /// approximately the depth shown"). Never normalised — see the type doc.</summary>
        public readonly float DepthMetres;

        /// <summary>
        /// Density: how many fish are showing. The owner's ruling in one number — "an area showing one
        /// fish (lower bite rate), several (higher)" — so this is BOTH how many marks the glass draws and
        /// the expected bite rate the fishing path multiplies by. One number, two consumers: that is the
        /// honesty invariant, and the reason a separate "bite rate" field is deliberately absent.
        /// </summary>
        public readonly int MarkCount;

        /// <summary>
        /// Which species are in this school, by stable Def id (rule 2 — ids, never Def references, so Core
        /// carries no content dependency). The fishing path weights its species roll by this set.
        ///
        /// <para><b>Empty or null means "no species stated"</b>, not "no fish": a consumer that finds no
        /// species falls back to its own normal roll for the place and time rather than refusing to bite.
        /// A real producer should always state at least one — an unstated school still raises the bite
        /// rate but says nothing about what is down there.</para>
        /// </summary>
        public readonly IReadOnlyList<string> SpeciesIds;

        /// <summary>Game seconds (the <c>IGameClock.TotalSeconds</c> frame) the school appears at.</summary>
        public readonly double StartSeconds;

        /// <summary>Game seconds the school is gone by. The window is <b>half-open</b>
        /// <c>[Start, End)</c>, so two back-to-back windows never both count the instant between
        /// them.</summary>
        public readonly double EndSeconds;

        public FishSchool(Vector2 centre, float radiusMetres, float depthMetres, int markCount,
                          IReadOnlyList<string> speciesIds, double startSeconds, double endSeconds)
        {
            Centre = centre;
            RadiusMetres = radiusMetres;
            DepthMetres = depthMetres;
            MarkCount = markCount;
            SpeciesIds = speciesIds;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
        }

        /// <summary>Is the school's window open at this game time? Half-open <c>[Start, End)</c>.</summary>
        public bool IsActiveAt(double gameSeconds)
            => gameSeconds >= StartSeconds && gameSeconds < EndSeconds;

        /// <summary>Is this world position ON the school (inside its area)? Squared-distance compare — no
        /// square root on the hot path.</summary>
        public bool Contains(Vector2 worldPos)
        {
            if (RadiusMetres <= 0f) return false;
            float dx = worldPos.x - Centre.x;
            float dy = worldPos.y - Centre.y;
            return dx * dx + dy * dy <= RadiusMetres * RadiusMetres;
        }

        /// <summary>
        /// How solid the return is at this position: <b>1 at the centre, falling linearly to 0 at the
        /// rim</b>, 0 outside. Dimensionless by nature (it is a signal strength, not a length), so unlike a
        /// depth it is legitimately 0..1 on both sides of the seam.
        ///
        /// <para>Lets the finder dim a school you are only clipping the edge of, and lets the fishing path
        /// scale the bite rate by how well you are sitting on it — again from ONE number, so the picture
        /// and the fishing agree.</para>
        /// </summary>
        public float SignalAt(Vector2 worldPos)
        {
            if (RadiusMetres <= 0f) return 0f;
            float dx = worldPos.x - Centre.x;
            float dy = worldPos.y - Centre.y;
            float signal = 1f - Mathf.Sqrt(dx * dx + dy * dy) / RadiusMetres;
            if (signal <= 0f) return 0f;
            return signal >= 1f ? 1f : signal;
        }

        /// <summary>Is this species in the school? Ordinal compare on the stable id; false for a null/empty
        /// set or a null id (see <see cref="SpeciesIds"/> on what an empty set means).</summary>
        public bool HasSpecies(string speciesId)
        {
            if (SpeciesIds == null || string.IsNullOrEmpty(speciesId)) return false;
            for (int i = 0; i < SpeciesIds.Count; i++)
                if (string.Equals(SpeciesIds[i], speciesId, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>This school as the finder sees it from <paramref name="worldPos"/> — the one
        /// derivation both the empty default and a real model use, so a mark can never be built two
        /// ways.</summary>
        public FishMark ToMark(Vector2 worldPos)
            => new FishMark(DepthMetres, MarkCount, SignalAt(worldPos));
    }

    /// <summary>
    /// What the fish finder's glass gets handed for one school in range: a depth <b>in metres</b>, how many
    /// fish to draw, and how solid the return is.
    ///
    /// <para><b>One record per school, not per fish.</b> <see cref="Count"/> is how many fish icons the
    /// glass draws for this return — which is the same number that drives the bite rate
    /// (<see cref="FishSchool.MarkCount"/>). Where each icon sits HORIZONTALLY on the scrolling scan, and
    /// any jitter between them, is presentation the finder owns; the seam states the school's depth once
    /// and does not pretend to know which pixel column a fish is in.</para>
    ///
    /// <para><b>Metres, not 0..1.</b> The rig places marks in normalised column space
    /// (<c>fishRig.js</c>: <c>y·range</c> → a depth), so the presenter divides by the player's current
    /// RANGE at paint time. Storing the normalised value here would bake in a range that the RANGE pushers
    /// change a moment later.</para>
    /// </summary>
    public readonly struct FishMark
    {
        /// <summary>Depth of the return below the surface, in metres.</summary>
        public readonly float DepthMetres;

        /// <summary>How many fish this return is worth (see the type doc — also the bite-rate driver).</summary>
        public readonly int Count;

        /// <summary>Signal strength 0..1 — 1 sitting on the school's centre, 0 at its rim
        /// (<see cref="FishSchool.SignalAt"/>).</summary>
        public readonly float Strength;

        public FishMark(float depthMetres, int count, float strength)
        {
            DepthMetres = depthMetres;
            Count = count;
            Strength = strength;
        }
    }
}
