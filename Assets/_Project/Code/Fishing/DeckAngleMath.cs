using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The PURE maths of the <b>deck-angle fight term</b> (Rod Fishing v2 §4.2 — the owner's locked
    /// "light real factor"): where the angler STANDS on the walkable deck, against where the fish is,
    /// becomes a slow extra tension pressure when the line would run ACROSS the hull — and walking the
    /// rail to a clean angle relieves it. Movement is a real fight input, not just freedom.
    ///
    /// <para><b>The read (scale-free by construction).</b> Both points go into the DECK FRAME (x abeam,
    /// y along the keel toward the drawn bow — the exact frame the Player lane's deck-walk clamps in).
    /// <see cref="AcrossHull01"/> then casts the line's ray from the angler toward the fish and measures
    /// how much of the deck rectangle it crosses before it leaves the boat, as a fraction of the
    /// rectangle's full chord along that line: standing at the rail the fish is off → 0 (the line drops
    /// straight over the side, clean); standing at the OPPOSITE rail with the whole hull between you and
    /// her → 1 (the worst stance). It is continuous and monotone as the angler walks, so "walk the rail
    /// and it eases" falls out of the geometry — the cozy prompt IS the relief gradient. No line-cutting,
    /// no snag damage: a bad stance only pressures the same tension gauge every other mistake does
    /// (owner's cozy rule).</para>
    ///
    /// <para><b>Lane discipline</b> (the <see cref="RodFightMath"/> twin): pure, static, engine-light, no
    /// RNG, no clock, nothing saved, NaN-safe — a closed-form function of its inputs, fully
    /// EditMode-testable. The tuning constant (<c>GameConfig.RodFight.DeckAngleFactor</c>) arrives as a
    /// plain float; <b>0 = the term is OFF</b> and a deck fight reads exactly like the dock (the owner's
    /// dock-parity check, rule 6 — no magic numbers).</para>
    /// </summary>
    public static class DeckAngleMath
    {
        /// <summary>
        /// A world-axis offset expressed in the drawn hull's DECK FRAME (x abeam, y along the keel
        /// toward the bow) for a hull drawn at compass heading <paramref name="drawnHeadingDeg"/>
        /// (0 = North, 90 = East, clockwise — the project's bearing convention). Kept numerically
        /// IDENTICAL to the Player lane's deck-walk transform (<c>DeckWalkController.WorldToDeckFrame</c>
        /// — pinned by a parity test) so the fight measures the very rectangle the player walks; the
        /// module boundary (rule 4) is why the four lines live twice. Pure, NaN-safe.
        /// </summary>
        public static Vector2 WorldToDeckFrame(Vector2 worldOffset, float drawnHeadingDeg)
        {
            float rad = Safe(drawnHeadingDeg) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            float x = Safe(worldOffset.x), y = Safe(worldOffset.y);
            return new Vector2(x * cos - y * sin, x * sin + y * cos);
        }

        /// <summary>
        /// How much of the hull the line crosses from this stance, 0 (clean — the fish is straight off
        /// the rail you stand at) → 1 (the worst stance — the whole deck lies between you and her).
        /// Deck-frame in, fraction out: the exit distance of the angler→fish ray through the deck
        /// rectangle, over the rectangle's full chord along that line — so the read is scale-free
        /// (a punt and a dragger grade their rails identically) and independent of how FAR out the fish
        /// is (angle is the skill axis, distance is the cast's). An angler read outside the rectangle is
        /// clamped onto it (a transient clamp/rounding excursion never spikes the term). Degenerate
        /// inputs (fish on top of the angler, a collapsed rectangle) read 0 — neutral, never a hidden
        /// penalty. Pure, deterministic, NaN-safe.
        /// </summary>
        /// <param name="anglerDeck">The angler's position in the deck frame (m).</param>
        /// <param name="fishDeck">The fish / line's far end in the deck frame (m).</param>
        /// <param name="deckCenter">Centre of the walkable deck rectangle (deck frame, m).</param>
        /// <param name="deckHalfExtents">Half-extents of the deck rectangle (m; x = half-beam, y = half-length).</param>
        public static float AcrossHull01(Vector2 anglerDeck, Vector2 fishDeck,
                                         Vector2 deckCenter, Vector2 deckHalfExtents)
        {
            float hx = Mathf.Abs(Safe(deckHalfExtents.x));
            float hy = Mathf.Abs(Safe(deckHalfExtents.y));
            if (hx <= 1e-6f || hy <= 1e-6f) return 0f;   // a collapsed deck grades nothing

            // Work relative to the rectangle centre; hold the angler ON the deck (they always are —
            // the deck-walk clamps every step — but a stale/rounded read must not break the slab casts).
            Vector2 p = new Vector2(
                Mathf.Clamp(Safe(anglerDeck.x) - Safe(deckCenter.x), -hx, hx),
                Mathf.Clamp(Safe(anglerDeck.y) - Safe(deckCenter.y), -hy, hy));

            Vector2 dir = new Vector2(Safe(fishDeck.x) - Safe(anglerDeck.x),
                                      Safe(fishDeck.y) - Safe(anglerDeck.y));
            if (dir.sqrMagnitude <= 1e-8f) return 0f;    // no direction to grade — neutral

            float exit = RayExit(p, dir, hx, hy);        // deck crossed on the way OUT toward the fish
            float back = RayExit(p, -dir, hx, hy);       // …and the room behind, to the opposite rail
            float chord = exit + back;
            if (chord <= 1e-6f || float.IsInfinity(chord)) return 0f;
            return Mathf.Clamp01(exit / chord);
        }

        /// <summary>
        /// The deck-angle tension pressure this stance adds, 0..1-gauge units per second — the additive
        /// term <see cref="RodFightMath.TensionRatePerSec"/> reserved its seam for: the owner's
        /// <paramref name="deckAngleFactor"/> (the pressure at the WORST stance) scaled by how badly the
        /// line crosses the hull. <b>factor 0 = OFF</b> — deck fights read exactly like the dock. Pure,
        /// NaN-safe, never negative (a clean stance relieves to zero; it never pays the angler).
        /// </summary>
        public static float TensionPerSec(float acrossHull01, float deckAngleFactor)
            => Mathf.Max(0f, Safe(deckAngleFactor)) * Mathf.Clamp01(Safe(acrossHull01));

        // ---- internals ---------------------------------------------------------------------------

        /// <summary>Distance along <paramref name="dir"/> (in units of |dir|·t — consistent across both
        /// casts, so the ratio is scale-free) from a point INSIDE the rectangle to its boundary. The
        /// standard slab test, one-sided: numerators are non-negative for an inside point.</summary>
        private static float RayExit(Vector2 p, Vector2 dir, float hx, float hy)
        {
            float tx = dir.x > 1e-9f ? (hx - p.x) / dir.x
                     : dir.x < -1e-9f ? (-hx - p.x) / dir.x
                     : float.PositiveInfinity;
            float ty = dir.y > 1e-9f ? (hy - p.y) / dir.y
                     : dir.y < -1e-9f ? (-hy - p.y) / dir.y
                     : float.PositiveInfinity;
            return Mathf.Min(tx, ty);
        }

        /// <summary>NaN → 0 (the safe, neutral value) — the module's standard input guard (rule 5).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
