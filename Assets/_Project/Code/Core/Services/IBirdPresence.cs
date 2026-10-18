using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What a bird near the view is doing, as far as a listener is concerned. Feeding birds call the
    /// most, birds in the air less, and birds sitting still the least.
    /// </summary>
    public enum BirdActivity : byte
    {
        /// <summary>Sitting: on the ground, on the water, on a perch, or asleep at a roost.</summary>
        Resting = 0,

        /// <summary>In the air: wheeling over its place or flying between places.</summary>
        Aloft = 1,

        /// <summary>Working food: at a fish plant or a wharf in its busy hours, or over a surface school.</summary>
        Feeding = 2,
    }

    /// <summary>
    /// One bird the player could hear: where its flock last placed it, what it is doing, which bird it
    /// is, and what species it is. A plain value; copying it allocates nothing, because the species id
    /// is a reference to a string the flock already holds.
    /// </summary>
    public readonly struct BirdAudible
    {
        /// <summary>The bird's ground point, world metres (the frame every key-scene piece uses).</summary>
        public readonly Vector2 WorldPosition;

        /// <summary>What it is doing.</summary>
        public readonly BirdActivity Activity;

        /// <summary>Stable for the bird (its place and slot) while it stays near, so a voice can follow
        /// the same bird from one sample to the next.</summary>
        public readonly int Key;

        /// <summary>The species, as a <c>bird.</c> id (<c>bird.herring_gull</c>). A listener voices only
        /// the species it has calls for.</summary>
        public readonly string SpeciesId;

        public BirdAudible(Vector2 worldPosition, BirdActivity activity, int key, string speciesId)
        {
            WorldPosition = worldPosition;
            Activity = activity;
            Key = key;
            SpeciesId = speciesId;
        }
    }

    /// <summary>
    /// THE BIRD-PRESENCE SEAM — the birds near the view right now, read-only. A flock writes it, the
    /// audio reads it, and neither names the other (rule 4).
    ///
    /// <para><b>A query, not an event stream.</b> Silence is a state: "no bird near" is
    /// <see cref="NearCount"/> == 0, which a test can check, where a stream of call events could only
    /// ever be absent. The listener owns its own cadence, voices and mix; the flock owns where the birds
    /// are. Positions are the flock's last schedule sample (a few times a second), which is plenty for
    /// calls that last a second or more.</para>
    ///
    /// <para><b>Reads, never state.</b> The birds are a function of <c>(worldSeed, gameTime)</c> and the
    /// view; nothing here enters the save (rule 5).</para>
    ///
    /// <para><b>No allocation on the read path</b> (rule 7): <see cref="CopyNear"/> fills a caller-owned
    /// array and returns how many it wrote.</para>
    /// FLAG lead-architect: new Core contract (the bird-presence seam).
    /// </summary>
    public interface IBirdPresence
    {
        /// <summary>The view rectangle, world metres, that the near set was taken against.</summary>
        Rect ViewWorld { get; }

        /// <summary>Every near bird, drawn or only counted.</summary>
        int NearCount { get; }

        /// <summary>
        /// Copies the near birds into <paramref name="into"/>, nearest the view's centre first, up to its
        /// length, and returns how many were written. A null array writes nothing and returns 0.
        /// </summary>
        int CopyNear(BirdAudible[] into);
    }

    /// <summary>
    /// THE EMPTY SKY — the null-object <see cref="IBirdPresence"/>: no birds, anywhere.
    /// <see cref="GameServices.BirdPresence"/> is this until a flock registers, and falls back to it
    /// the moment one goes away.
    ///
    /// <para><b>An empty sky is a reading, not a fault.</b> It is the correct shipped behaviour wherever
    /// no flock runs: the title screen, a bare art scene, an EditMode rig, a flock a fixture has stood
    /// down. It is also what a listener should hear there: nothing.</para>
    ///
    /// <para>Stateless and immutable, so the one <see cref="Instance"/> is safe to share.</para>
    /// </summary>
    public sealed class EmptyBirdPresence : IBirdPresence
    {
        /// <summary>The shared instance. There is no state to give a second one.</summary>
        public static readonly EmptyBirdPresence Instance = new EmptyBirdPresence();

        private EmptyBirdPresence() { }

        /// <inheritdoc/>
        public Rect ViewWorld => default;

        /// <inheritdoc/>
        public int NearCount => 0;

        /// <inheritdoc/>
        public int CopyNear(BirdAudible[] into) => 0;
    }
}
