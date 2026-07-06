namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A tiny <b>process-stable</b> hash (FNV-1a with an avalanche finalizer) for deriving a deterministic
    /// seed from content facts — currently the placed-trap catch seed, folded from
    /// <c>(worldSeed, instanceId, placementGameTimeSeconds)</c>.
    ///
    /// <para><b>Why not <c>string.GetHashCode()</c>.</b> On modern .NET (and Unity's runtime)
    /// <c>string.GetHashCode()</c> is <b>randomized per process</b> (a security default), so the same string
    /// hashes to a different int in a later run. A trap's catch is resolved on-demand from a seed — if that
    /// seed shifted between runs, a save→load would land a <em>different</em> catch, breaking rule 5
    /// determinism (the whole point of the trap runtime: "reload = identical catch"). FNV-1a over the raw
    /// UTF-16 code units is fully deterministic, allocation-free, and adequate for seeding an RNG (we need
    /// reproducibility + good spread, not cryptographic strength). It mirrors the FNV-1a scatter hash the
    /// St Peters clam field already uses (<c>StPetersBuilder.Hash01</c>) — the same constants, one home.</para>
    /// </summary>
    public static class StableHash
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>FNV-1a over the UTF-16 code units of <paramref name="s"/>, seeded from
        /// <paramref name="hash"/> so callers can chain fields. A null string folds in nothing (so a null
        /// and an empty string hash identically — both are "no text"). <c>unchecked</c> so the wraps are
        /// intentional, not overflow bugs.</summary>
        public static uint Fold(uint hash, string s)
        {
            unchecked
            {
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                    {
                        char c = s[i];
                        hash = (hash ^ (byte)(c & 0xFF)) * FnvPrime;   // low byte
                        hash = (hash ^ (byte)(c >> 8)) * FnvPrime;     // high byte
                    }
                }
                return hash;
            }
        }

        /// <summary>Fold an int into the running hash (FNV-1a over its four bytes).</summary>
        public static uint Fold(uint hash, int value)
        {
            unchecked
            {
                uint u = (uint)value;
                hash = (hash ^ (u & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 8) & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 16) & 0xFF)) * FnvPrime;
                hash = (hash ^ ((u >> 24) & 0xFF)) * FnvPrime;
                return hash;
            }
        }

        /// <summary>Fold a long into the running hash (FNV-1a over its eight bytes). Used to fold the exact
        /// bits of the placement time (via <see cref="System.BitConverter.DoubleToInt64Bits"/>) so a
        /// sub-second difference in placement yields a different seed.</summary>
        public static uint Fold(uint hash, long value)
        {
            unchecked
            {
                ulong u = (ulong)value;
                for (int b = 0; b < 8; b++)
                {
                    hash = (hash ^ (uint)(u & 0xFF)) * FnvPrime;
                    u >>= 8;
                }
                return hash;
            }
        }

        /// <summary>Apply the avalanche finalizer (the same mix the clam scatter uses) so close inputs
        /// diverge well — good spread for seeding a <see cref="System.Random"/>.</summary>
        public static uint Finalize(uint hash)
        {
            unchecked
            {
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                return hash;
            }
        }

        /// <summary>
        /// The trap catch seed: fold <c>(worldSeed, instanceId, placementGameTimeSeconds)</c> into a stable
        /// 32-bit value, then avalanche. Same inputs ⇒ same seed, this run and every future run — so the
        /// on-demand catch is bit-reproducible across a save→load (rule 5). The placement time is folded by
        /// its exact IEEE-754 bits so two traps placed a fraction of a second apart get different streams.
        /// </summary>
        public static int TrapCatchSeed(int worldSeed, string instanceId, double placementGameTimeSeconds)
        {
            unchecked
            {
                uint h = FnvOffsetBasis;
                h = Fold(h, worldSeed);
                h = Fold(h, instanceId);
                h = Fold(h, System.BitConverter.DoubleToInt64Bits(placementGameTimeSeconds));
                return (int)Finalize(h);
            }
        }
    }
}
