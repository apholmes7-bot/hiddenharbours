using UnityEngine;              // Mathf — the helm-framing ladder below
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Which of the follow-cam's discrete, pixel-perfect framings should be on screen. The camera never
    /// zooms to an arbitrary orthographic size — each framing maps to a PPU-integer step (the ratified
    /// per-context discrete-zoom vision), so the picture stays crisp at every stop.
    /// </summary>
    public enum CameraFraming
    {
        /// <summary>At the helm — the active hull's data-driven framing
        /// (<c>BoatHullDef.CameraWorldHeightMeters</c>; bigger boat = more water).</summary>
        Boat,

        /// <summary>Walking the coast — the tighter on-foot framing (the fisher reads large).</summary>
        OnFoot,

        /// <summary>Standing ON DECK (boarded, not at the helm) — a step closer again so the boat fills
        /// the screen and deck work (pots, bait, the rail) reads in detail. Owner playtest 2026-07-08.</summary>
        Deck,

        /// <summary>On deck with a trap haul LIVE — one more step so the rope-and-buoy action is the
        /// star. Released the moment the pot surfaces or the haul goes idle. Optional (owner-tunable).</summary>
        DeckHaul,
    }

    /// <summary>
    /// The zoom brain of <see cref="CameraFollow"/> as a plain engine-light POCO (CLAUDE.md §5): it maps
    /// (control mode, live-haul flag) to the <see cref="CameraFraming"/> that should be on screen, and
    /// owns the commit HOLD (hysteresis) so rapid helm⇄deck hops collapse into a single re-zoom instead
    /// of thrashing the discrete pixel-perfect steps. Deterministic — a pure function of the inputs and
    /// the time it is fed (no hidden clock, no randomness), so EditMode tests drive it with plain numbers.
    /// </summary>
    public sealed class CameraZoomPolicy
    {
        private bool _hasCommitted;
        private CameraFraming _committed;
        private double _lastCommitTime;

        /// <summary>False until the first <see cref="TryCommit"/> succeeds.</summary>
        public bool HasCommitted => _hasCommitted;

        /// <summary>The framing last committed (undefined until <see cref="HasCommitted"/>).</summary>
        public CameraFraming Committed => _committed;

        /// <summary>
        /// The framing the current control state WANTS. Pure mapping: the helm gets the boat's framing,
        /// on foot gets the on-foot framing, the deck gets the closer deck step — tightened one more step
        /// while a trap haul is live (if <paramref name="haulTightensZoom"/>; the haul is deck work, so
        /// the flag can never tighten any other mode).
        /// </summary>
        public static CameraFraming DesiredFraming(ControlMode mode, bool haulLive, bool haulTightensZoom)
        {
            if (mode == ControlMode.Aboard) return CameraFraming.Boat;
            if (mode == ControlMode.OnDeck)
                return (haulLive && haulTightensZoom) ? CameraFraming.DeckHaul : CameraFraming.Deck;
            return CameraFraming.OnFoot;
        }

        /// <summary>
        /// Feed the desired framing every tick; returns true exactly when the camera should re-frame NOW.
        /// The first-ever desire commits immediately (a single clean switch feels instant). A change that
        /// lands within <paramref name="minHoldSeconds"/> of the previous commit is HELD: keep feeding it
        /// and it commits the moment the hold expires — unless the desire meanwhile returns to the
        /// committed framing, in which case the hop dissolves and the camera never moved (a rapid
        /// helm⇄deck there-and-back re-zooms ZERO times).
        /// </summary>
        public bool TryCommit(CameraFraming desired, double nowSeconds, double minHoldSeconds)
        {
            if (_hasCommitted && desired == _committed) return false;
            if (_hasCommitted && nowSeconds - _lastCommitTime < minHoldSeconds) return false;
            _committed = desired;
            _hasCommitted = true;
            _lastCommitTime = nowSeconds;
            return true;
        }

        // ================= HELM FRAMING: the whole vessel visible (owner ruling 2026-07-29, §9.8) ==========
        //
        // *"cameras should zoom out on larger vessels so the whole vessels are visible; they seem fine
        // up till lobster boat and then you're too zoomed in on larger vessels."*
        //
        // ⚠️ THE DEFECT WAS A SILENT CAP, not a missing derivation. The step search ran integer
        // UPSCALE only (zoom x1..x8), and PixelPerfectCamera's own zoom clamps at >= 1 — so the widest
        // framing expressible AT ALL was screenH / ppu = 33.75 m at 1080p. The defs already ask for
        // 40 / 60 / 90 / 160 m for dragger -> tanker; every one of them was quietly capped to 33.75
        // and rendered at the SAME framing. That is exactly what he saw.
        //
        // ⚠️ AND THE RULING'S TWO CONSTRAINTS CANNOT BOTH HOLD ABOVE ~37.5 m OF HULL. "Whole vessel
        // visible" and "an integer pixel-perfect step" are unsatisfiable past that at the locked
        // PPU 32 (VS-23): a 110 m tanker is 3,520 asset pixels of hull on a 1,920 px screen. The ladder
        // therefore continues OUTWARD by integer DOWNSCALE (2:1, 3:1 …) — still a clean pixel ratio,
        // 2x2 asset pixels to one screen pixel, with no blur and no shimmer, unlike an arbitrary ortho
        // size. Coordinator's call, 2026-07-30: big hulls lose pixel DETAIL rather than being cropped.

        /// <summary>Ladder steps are a signed integer. <c>step &gt;= 1</c> is integer UPSCALE (zoom
        /// in): <c>screenH / (step*ppu)</c>. <c>step &lt;= -2</c> is integer DOWNSCALE (zoom out):
        /// <c>screenH * (-step) / ppu</c>. Step 1 is the 1:1 pivot, so -1 is deliberately not a step —
        /// it would duplicate it.</summary>
        public const int MinStep = -8;   // 8:1 downscale — 270 m of world height at 1080p
        public const int MaxStep = 8;    // x8 upscale — the tightest haul framing

        /// <summary>World height (m) a ladder step shows. The ONE place the ladder is defined.</summary>
        public static float WorldHeightForStep(int step, int ppu, int screenHeightPx)
        {
            int p = Mathf.Max(1, ppu);
            float h = Mathf.Max(1, screenHeightPx);
            if (step >= 1) return h / (step * p);            // integer upscale (crisp, magnified)
            return h * Mathf.Max(2, -step) / p;              // integer downscale (clean ratio, shrunk)
        }

        /// <summary>True when a step is an integer UPSCALE — the only kind <c>PixelPerfectCamera</c>
        /// can express (its zoom clamps at &gt;= 1). A downscale step must bypass it and drive the
        /// orthographic size directly; see <c>CameraFollow.ApplyFramingHard</c>.</summary>
        public static bool StepIsPixelPerfectUpscale(int step) => step >= 1;

        /// <summary>The ladder step nearest a requested world height — now searching OUTWARD as well
        /// as in, so a def asking for 60 m is no longer silently served 33.75.</summary>
        public static int StepForWorldHeight(float worldHeightMeters, int ppu, int screenHeightPx)
        {
            float wanted = Mathf.Max(0.5f, worldHeightMeters);
            int best = 1;
            float bestErr = float.MaxValue;
            for (int s = MinStep; s <= MaxStep; s++)
            {
                if (s == -1 || s == 0) continue;             // not steps (see the const doc)
                float err = Mathf.Abs(WorldHeightForStep(s, ppu, screenHeightPx) - wanted);
                if (err < bestErr) { bestErr = err; best = s; }
            }
            return best;
        }

        /// <summary>
        /// The helm framing (m of world height) for a hull: its authored framing, FLOORED by what it
        /// takes to actually show the vessel.
        ///
        /// <para><b>A floor, not a replacement</b> — and that is why the small end is untouched. A
        /// dory is framed at 14 m for intimacy (P2's scale fantasy), not because 4.5 m of boat needs
        /// it; replacing the authored value with a fit-derived one would zoom every small boat IN and
        /// change the game the owner is happy with. The ruling says only that big vessels must not be
        /// cropped, so this only ever pushes OUT.</para>
        ///
        /// <para><b>The worst-case footprint is not the hull's length.</b> The view is iso: bow-on, a
        /// hull is foreshortened into <c>sin(elevation)</c> of the screen's SHORT axis; beam-on it
        /// lies across the LONG axis and costs <c>1/aspect</c> of the height. The binding case is
        /// whichever is larger — at the fleet's 40° elevation on 16:9 that is the bow-on 0.643, and
        /// measuring against raw length instead would zoom out ~55 % further than any heading needs.</para>
        /// </summary>
        public static float HelmWorldHeightMeters(float authoredWorldHeightMeters, float hullLengthMeters,
                                                  float marginFactor, float isoElevationDegrees, float aspect)
        {
            float footprint = Mathf.Max(Mathf.Sin(Mathf.Max(1f, isoElevationDegrees) * Mathf.Deg2Rad),
                                        1f / Mathf.Max(0.1f, aspect));
            float needed = Mathf.Max(0f, hullLengthMeters) * Mathf.Max(1f, marginFactor) * footprint;
            return Mathf.Max(authoredWorldHeightMeters, needed);
        }
    }
}
