using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where in her period each lit mark sits, so that no two of one character wink together.</b>
    /// A pure function of the CHART — the set of mark ids and what each one shows — and of nothing
    /// else.
    ///
    /// <para><b>⭐ Why a plan at all, when a hash of the mark's own id is simpler.</b> That was the
    /// first design and it was measured, not assumed: hashing each id independently spreads the
    /// marks UNIFORMLY, and a uniform spread of a handful of points has small gaps in it by the
    /// birthday problem. On the twenty-five marks actually placed in the two harbours it put
    /// <c>channel.nmc_entrance.p0</c> and <c>channel.nmc_bar_gut.p1</c> — two port-hand cans in one
    /// harbour, both <c>Fl G 4s</c> — <b>0.021 s apart on a four-second period</b>. That is unison to
    /// any eye, in one frame, which is the one picture this whole feature exists to avoid. The test
    /// that found it is <c>NoTwoMarksOfOneCharacterFlashInUnisonInOneRegion</c>.</para>
    ///
    /// <para><b>The fix is to SHARE OUT the period instead of sampling it.</b> Marks wearing one
    /// character are sorted by their chart id and given a slot each, so <c>k</c> marks are
    /// <c>period/k</c> apart at worst instead of however close chance put them. A hash is still used,
    /// but only to JITTER each mark inside its own slot — because a perfectly even round-robin of six
    /// green flashes reads as a marquee, and real marks are not in step in that way either. The
    /// jitter is bounded so the guaranteed gap survives it.</para>
    ///
    /// <para><b>⚠️ It is still not a spawn order, and that is the whole discipline.</b> The slots are
    /// handed out in SORTED-ID order, so the marks may be placed in any sequence whatever and every
    /// one of them gets the same phase back. That is the property the lamp lane already paid for
    /// once: a scene light seeded its flicker off <c>transform.GetSiblingIndex()</c>, and adding a
    /// single child re-seeded every light beside it, reddening a fixture that had been green for a
    /// fortnight.</para>
    ///
    /// <para><b>⚠️ What this does NOT promise, stated plainly: adding a mark re-slots her whole
    /// character group.</b> Six green cans share the period six ways; a seventh makes it sevenths and
    /// every one of them moves. That is a real difference from hashing each id alone, and it is
    /// accepted rather than hidden, because the two properties are not equally worth having. Order
    /// independence is what stops a picture changing for a reason nobody can see; per-mark stability
    /// across an edited chart would only matter if a phase were saved or compared across versions,
    /// and it is neither — the spread is recomputed from scratch every time a region is built, and a
    /// mark's phase is not state anybody may rely on (rule 5).</para>
    ///
    /// <para><b>Never saved (rule 5).</b> The fractions are computed at placement and serialised onto
    /// the placed mark exactly as her facing and her size rung are — derived content, not state.</para>
    /// </summary>
    public static class NavLightPhasePlan
    {
        /// <summary>
        /// How far a mark may be jittered inside her slot, as a fraction of the slot. At 0.2 the
        /// worst gap between two neighbours is 0.6 of a slot rather than a full one — irregular
        /// enough not to read as a machine, wide enough that the guarantee still holds.
        /// </summary>
        public const float JitterFractionOfSlot = 0.2f;

        /// <summary>
        /// The worst gap this plan guarantees between two marks of one character, as a fraction of
        /// the period, for <paramref name="count"/> marks sharing it. Exposed so a test can state the
        /// bound rather than rediscover it.
        /// </summary>
        public static float GuaranteedGapFraction(int count) =>
            count < 2 ? 1f : (1f - 2f * JitterFractionOfSlot) / count;

        /// <summary>
        /// Share out the period among the marks that wear each character.
        ///
        /// <para>Returns a map from mark id to a phase FRACTION in <c>[0, 1)</c> — a fraction rather
        /// than seconds, because the same character may be worn at different periods one day and the
        /// fraction stays right. Marks whose character is empty are unlit and do not appear.</para>
        ///
        /// <para>Call it once per REGION. Two marks the player can never see at once do not need to
        /// be told apart, and pooling every harbour in the game into one spread would make each
        /// harbour's own marks tighter for nothing.</para>
        /// </summary>
        public static Dictionary<string, float> Spread(IEnumerable<(string Id, string Character)> marks)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (marks == null) return result;

            var byCharacter = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach ((string Id, string Character) m in marks)
            {
                if (string.IsNullOrEmpty(m.Id) || string.IsNullOrWhiteSpace(m.Character)) continue;
                if (!byCharacter.TryGetValue(m.Character, out List<string> list))
                    byCharacter[m.Character] = list = new List<string>();
                if (!list.Contains(m.Id)) list.Add(m.Id);
            }

            foreach (KeyValuePair<string, List<string>> kv in byCharacter)
            {
                List<string> ids = kv.Value;
                // ⚠️ ORDINAL, and sorted. This is the line that makes the answer independent of the
                // order the marks arrived in — which is the whole reason a plan is allowed to exist.
                ids.Sort(StringComparer.Ordinal);

                int count = ids.Count;
                for (int i = 0; i < count; i++)
                {
                    float slot = i / (float)count;
                    float jitter = Jitter01(ids[i]) * (JitterFractionOfSlot / count);
                    float phase = slot + jitter;
                    result[ids[i]] = phase - (float)Math.Floor(phase);   // into [0, 1)
                }
            }

            return result;
        }

        /// <summary>A deterministic offset in <c>[-1, 1)</c> from a mark's id — FNV-1a, so it is the
        /// same on every platform forever (see <see cref="NavLightCharacter.SeedFromId"/>).</summary>
        private static float Jitter01(string id)
        {
            int seed = NavLightCharacter.SeedFromId(id);
            // The low bits of FNV are the well-mixed ones; take 16 of them and centre on zero.
            int bits = (seed & 0xFFFF);
            return bits / 32768f - 1f;
        }
    }
}
