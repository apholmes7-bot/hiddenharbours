using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The pure, deterministic derivations for the post-haul <b>deck work</b> (trap-fishing arc Build 7):
    /// per-animal size, berried flag, and the per-grab nip roll. Static + parameterised — fully
    /// EditMode-testable with no engine state, no clock, no scene.
    ///
    /// <para><b>Seed lineage (rule 5).</b> Every stream starts from the SAME seed the trap's catch was
    /// rolled from — <see cref="StableHash.TrapCatchSeed"/> over (worldSeed, instanceId, placement time) —
    /// then folds the animal's resolved identity (species id + index in the catch), a channel salt
    /// ("size" / "berried" / "nip"), and for nips the attempt index. Same placement facts ⇒ the identical
    /// animals with the identical sizes ⇒ the identical sort, this run and every future run. NOTHING here
    /// reads Time, the wave field, or any hidden global RNG.</para>
    ///
    /// <para><b>The care read (the owner's teeth, kept cozy).</b> A grab's nip chance eases with how FULL
    /// the hold was: release at the quick-grab threshold and you risk the rushed chance; hold to the full
    /// mark and it eases to the careful floor. The ROLL is a pure function of the seeds; only the
    /// THRESHOLD moves with the player's hands — deterministic sim, player-driven skill.</para>
    /// </summary>
    public static class DeckWork
    {
        /// <summary>Channel salt for the per-animal SIZE stream.</summary>
        public const string SizeChannel = "size";
        /// <summary>Channel salt for the per-animal BERRIED stream.</summary>
        public const string BerriedChannel = "berried";
        /// <summary>Channel salt for the per-grab NIP stream (fold the attempt index too).</summary>
        public const string NipChannel = "nip";

        /// <summary>
        /// The stable per-animal hash for one channel: the trap's catch seed (the same lineage the catch
        /// roll used) folded with the animal's resolved species id, its index in the catch, the channel
        /// salt, and the attempt (0 except for nip re-tries). Avalanched for spread. Same inputs ⇒ same
        /// hash, every run (see <see cref="StableHash"/> for why not string.GetHashCode()).
        /// </summary>
        public static uint AnimalHash(int worldSeed, string instanceId, double placementGameTimeSeconds,
                                      string speciesId, int animalIndex, string channel, int attempt = 0)
        {
            unchecked
            {
                uint h = (uint)StableHash.TrapCatchSeed(worldSeed, instanceId, placementGameTimeSeconds);
                h = StableHash.Fold(h, speciesId);
                h = StableHash.Fold(h, animalIndex);
                h = StableHash.Fold(h, channel);
                h = StableHash.Fold(h, attempt);
                return StableHash.Finalize(h);
            }
        }

        /// <summary>Map a hash to a uniform [0,1): the top 24 bits over 2^24 (exact in float — no
        /// precision loss, no modulo bias worth caring about for feel rolls).</summary>
        public static float U01(uint hash) => (hash >> 8) * (1f / 16777216f);

        /// <summary>The animal's deterministic size (mm) from its size-channel hash, uniform across the
        /// rule's window. A degenerate window (max ≤ min) collapses to min.</summary>
        public static float SizeMm(uint sizeHash, float sizeMinMm, float sizeMaxMm)
        {
            float lo = Mathf.Min(sizeMinMm, sizeMaxMm);
            float hi = Mathf.Max(sizeMinMm, sizeMaxMm);
            return lo + U01(sizeHash) * (hi - lo);
        }

        /// <summary>The animal's deterministic berried flag from its berried-channel hash. Only a species
        /// that can be berried ever is; chance is clamped to [0,1].</summary>
        public static bool RollBerried(uint berriedHash, bool canBeBerried, float berriedChance01)
            => canBeBerried && U01(berriedHash) < Mathf.Clamp01(berriedChance01);

        /// <summary>How CAREFUL a grab was, 0..1, from how long the hold lasted: 0 at (or under) the
        /// quick-grab threshold, 1 at the full-grab mark, linear between. Degenerate tuning (full ≤ quick)
        /// reads every counted grab as full-care (the forgiving collapse).</summary>
        public static float Care01(float heldSeconds, float quickGrabSeconds, float fullGrabSeconds)
        {
            if (fullGrabSeconds <= quickGrabSeconds) return 1f;
            return Mathf.Clamp01((heldSeconds - quickGrabSeconds) / (fullGrabSeconds - quickGrabSeconds));
        }

        /// <summary>The nip chance for a grab of the given care: the rushed chance eased linearly down to
        /// the careful floor. Both ends clamped to [0,1].</summary>
        public static float NipChance01(float care01, float rushedChance01, float carefulChance01)
            => Mathf.Lerp(Mathf.Clamp01(rushedChance01), Mathf.Clamp01(carefulChance01), Mathf.Clamp01(care01));

        /// <summary>Did THIS grab get nipped? The roll is the attempt's nip-channel hash against the
        /// care-eased chance. Deterministic: same animal + same attempt + same care ⇒ same answer.</summary>
        public static bool RollNip(uint nipHash, float nipChance01)
            => U01(nipHash) < Mathf.Clamp01(nipChance01);

        /// <summary>The sort verdict: a keeper is at legal size and NOT berried. (An animal whose species
        /// has no rule never reaches here — it's an always-keeper by convention.)</summary>
        public static bool IsKeeper(float sizeMm, bool berried, float minKeepSizeMm)
            => !berried && sizeMm >= minKeepSizeMm;
    }
}
