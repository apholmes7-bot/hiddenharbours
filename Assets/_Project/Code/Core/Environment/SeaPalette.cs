using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The live SEA PALETTE the water is drawing itself with this tick — the four art-directed anchor
    /// colours of the ADR 0015 palette guard-rail (<c>_PaletteDeep</c> / <c>_PaletteMid</c> /
    /// <c>_PaletteShallow</c> / <c>_PaletteFoam</c>), as the ACTIVE surface has them after its mood ease.
    ///
    /// <para><b>Why this exists.</b> The wake's foam is not a decal painted on the sea, it is water — so
    /// when it ages it must walk down <b>the water's own ramp</b>, never a set of hexes invented on a
    /// particle component (ADR 0015's whole point: the sea's output stays inside an art-directed palette,
    /// and a preset swap moves the palette). The foam particles are drawn by Boats and the palette is
    /// owned by Art, so the two meet HERE (CLAUDE.md rule 4) exactly as <see cref="DisplacedSea"/> carries
    /// the shared exaggeration.</para>
    ///
    /// <para><b>Publish the instance, never a copy.</b> The anchors are MOOD-EASED — <c>WaterSurface</c>
    /// blends them between preset materials as the weather turns, every frame. A consumer that read the
    /// material itself (or cached a copy at Awake) would disagree with the drawn sea whenever the mood
    /// moved, which is the stale-twin bug this repo has paid for more than once. The surface publishes its
    /// OWN eased values each push; consumers read THIS every tick and cache nothing.</para>
    ///
    /// <para><b>Presentation only (rule 5).</b> Colours feed no simulation, enter no save, and are
    /// recomputed by their publisher from the live weather each tick. Absent state is the OFF contract:
    /// a consumer with no published palette falls back to its own serialized colour and draws exactly
    /// what it drew before this seam existed.</para>
    /// </summary>
    public readonly struct SeaPaletteState
    {
        /// <summary>The DEEP-water anchor (<c>_PaletteDeep</c>) — the darkest blue in the ramp.</summary>
        public readonly Color Deep;

        /// <summary>The MID-water anchor (<c>_PaletteMid</c>).</summary>
        public readonly Color Mid;

        /// <summary>The SHALLOW-water anchor (<c>_PaletteShallow</c>).</summary>
        public readonly Color Shallow;

        /// <summary>The FOAM / highlight anchor (<c>_PaletteFoam</c>) — the white a churn is born at.</summary>
        public readonly Color Foam;

        public SeaPaletteState(Color deep, Color mid, Color shallow, Color foam)
        {
            Deep = deep;
            Mid = mid;
            Shallow = shallow;
            Foam = foam;
        }
    }

    /// <summary>
    /// The Core seam between the Art-side water surface (publisher) and its colour consumers — today the
    /// Boats-side wake foam, tomorrow any other water-riding visual that must age into the sea rather than
    /// into transparency. Mirrors <see cref="DisplacedSea"/> in shape and in ownership discipline: one
    /// publisher at a time, last-writer-wins, and only the current owner may clear.
    /// </summary>
    public static class SeaPalette
    {
        private static object s_Owner;
        private static SeaPaletteState s_State;

        /// <summary>True while an active water surface has published a palette.</summary>
        public static bool IsActive => s_Owner != null;

        /// <summary>The live palette; false (and <c>default</c>) when no surface has published one.</summary>
        public static bool TryGet(out SeaPaletteState state)
        {
            state = s_State;
            return s_Owner != null;
        }

        /// <summary>Publish the active surface's eased anchors (each uniform push — re-publishing is how a
        /// mood turn or a preset swap reaches the consumers).</summary>
        public static void Publish(object owner, in SeaPaletteState state)
        {
            if (owner == null) return;
            s_Owner = owner;
            s_State = state;
        }

        /// <summary>Clear the palette — only by its current owner, so a stale publisher going away cannot
        /// kill a newer sea's state. No palette ⇒ consumers draw their serialized fallback: the OFF
        /// contract.</summary>
        public static void Clear(object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_State = default;
        }
    }
}
