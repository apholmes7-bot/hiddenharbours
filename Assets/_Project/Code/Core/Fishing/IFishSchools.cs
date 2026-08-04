using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE FISH-SCHOOL SEAM (ADR 0025 S3) — where "there are fish here" is asked and answered.
    ///
    /// <para><b>Two lanes meet only here.</b> Gameplay <b>produces</b> schools (which fish are where, at
    /// what depth, for how long — a function of place, weather, date and the world seed) and reads
    /// <see cref="SchoolsAt"/> to raise the bite rate and weight the species roll. UI <b>reads</b>
    /// <see cref="MarksAt"/> to draw the sonar. Neither module references the other's concrete classes
    /// (rule 4) and neither owns a second copy of the fish — which is exactly the owner's honesty
    /// invariant: the thing on the glass IS the thing that changes the fishing.</para>
    ///
    /// <para><b>Both reads see the same schools.</b> <see cref="MarksAt"/> is not a second model; it is
    /// <see cref="SchoolsAt"/> put through <see cref="FishSchool.ToMark"/>. It exists so the finder does not
    /// have to know what a species set or a time window is, and so the UI assembly can be built and
    /// exercised against the seam alone.</para>
    ///
    /// <para><b>"At", not "near".</b> A school carries its own <see cref="FishSchool.RadiusMetres"/>, so
    /// the query is "which schools' areas contain this position, with their window open at this time" —
    /// there is no second search radius anywhere to drift out of sync with the one the fish are actually
    /// in. Deliberate: the player drives INTO an area and the marks appear.</para>
    ///
    /// <para><b>No allocation on the read path</b> (rule 7). Both calls fill a caller-owned list, cleared
    /// first, and return how many were written — the <c>InstrumentLocker.OwnedFor</c> shape. A finder
    /// repainting on its slow tick and a bite check on its own tick both reuse one list forever.</para>
    ///
    /// <para><b>Never saved</b> (rule 5). Schools are recomputed from <c>(worldSeed, gameTime)</c> + place
    /// + weather like the tide and the wind; nothing here has a save DTO and nothing on <c>SaveData</c>
    /// names a school. A saved school would be stale the moment the day turned.</para>
    /// FLAG lead-architect: new Core contract (the ADR 0025 S3 fish-school seam).
    /// </summary>
    public interface IFishSchools
    {
        /// <summary>
        /// The GAMEPLAY read: every school whose area contains <paramref name="worldPos"/> and whose
        /// window is open at <paramref name="gameSeconds"/> (the <c>IGameClock.TotalSeconds</c> frame).
        /// <paramref name="into"/> is cleared first and returned filled; the return value is how many were
        /// written. A null list is tolerated (nothing is written, the count is still correct).
        /// </summary>
        int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into);

        /// <summary>
        /// The UI read: the same schools as <see cref="SchoolsAt"/>, reduced to what the glass needs —
        /// one <see cref="FishMark"/> per school, <b>depth in METRES</b>. The presenter divides by the
        /// player's current RANGE at paint time to get the rig's normalised column position; a normalised
        /// depth is never stored on either side of this seam.
        /// </summary>
        int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into);
    }

    /// <summary>
    /// THE EMPTY SEA — the null-object <see cref="IFishSchools"/>: no schools, ever, anywhere.
    /// <see cref="GameServices.FishSchools"/> is this until a real model registers itself, and falls back
    /// to it again the moment one goes away.
    ///
    /// <para><b>This is what lets the finder and the fish be built in parallel.</b> The UI slice's host
    /// compiles, runs and draws an honest empty sonar with no fish model in the project at all; the
    /// gameplay slice swaps its model in — <c>GameServices.FishSchools = myModel;</c> — and the finder
    /// starts painting real marks without one line of UI changing. It is also the correct SHIPPED
    /// behaviour anywhere a model legitimately does not exist: a bare art scene, an EditMode rig, a region
    /// with no fish authored yet. An empty sonar is the truth in all of those; a
    /// <c>NullReferenceException</c> is not.</para>
    ///
    /// <para>Stateless and immutable, so the one <see cref="Instance"/> is safe to share.</para>
    /// </summary>
    public sealed class EmptyFishSchools : IFishSchools
    {
        /// <summary>The shared instance. There is no state to give a second one.</summary>
        public static readonly EmptyFishSchools Instance = new EmptyFishSchools();

        private EmptyFishSchools() { }

        /// <inheritdoc/>
        public int SchoolsAt(Vector2 worldPos, double gameSeconds, List<FishSchool> into)
        {
            into?.Clear();
            return 0;
        }

        /// <inheritdoc/>
        public int MarksAt(Vector2 worldPos, double gameSeconds, List<FishMark> into)
        {
            into?.Clear();
            return 0;
        }
    }
}
