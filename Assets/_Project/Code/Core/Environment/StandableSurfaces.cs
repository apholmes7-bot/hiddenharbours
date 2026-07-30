using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The <b>registry of standable structures</b> for the active region, plus the one composition every
    /// on-foot consumer reads: <see cref="OnFootDepth"/>. Where a registered <see cref="IStandableSurface"/>
    /// is present, a person's standing height is its DECK rather than the seabed beneath it — so the St
    /// Peters pier is walkable over its own dredged −1.0 m slip while the water under it stays exactly as
    /// deep as the terrain authored it.
    ///
    /// <para><b>A registry, not a <see cref="GameServices"/> slot.</b> Unlike <see cref="ITidalTerrain"/>
    /// (one per region, last writer wins) there are MANY standable surfaces at once — one wharf today, one
    /// per boat deck in M2 — appearing and disappearing with their components. So this mirrors
    /// <see cref="EventBus"/>: a static Core registry with <see cref="Register"/> /
    /// <see cref="Unregister"/> and no installer to wire. Registrants are scene-scoped and relinquish on
    /// disable, so a region teardown leaves an empty registry — i.e. "no structures", under which every
    /// composition below is <b>bit-identical</b> to the pure terrain-vs-water answer that predates this
    /// seam.</para>
    ///
    /// <para><b>ON FOOT ONLY (the boundary that matters).</b> Nothing here is read by the water render, the
    /// boat-cross/grounding depth, the clam-baring or the seabed bake. A pier does not shoal the berth
    /// beneath it, and a hull must still float in the water the terrain authored — see
    /// <see cref="IStandableSurface"/>.</para>
    ///
    /// <para><b>Pure &amp; deterministic (rule 5).</b> Every method takes its inputs explicitly and has a
    /// <c>Now</c> twin that pulls the live Core services, exactly as <c>TidalWalkability</c> does — so the
    /// whole rule is EditMode-testable headless with fake terrain / environment / surface doubles. No RNG,
    /// nothing saved, no allocation per query (the lookup walks the list by index).</para>
    /// </summary>
    public static class StandableSurfaces
    {
        // Small by construction: one wharf today; a boat deck each in M2. A plain list walked by index is
        // both the cheapest and the most predictable thing here (rule 7 — no per-query allocation, no
        // enumerator, no hashing).
        private static readonly List<IStandableSurface> Surfaces = new List<IStandableSurface>(4);

        /// <summary>How many surfaces are registered right now (0 = "no structures" — every composition
        /// below reduces to the plain terrain-vs-water answer).</summary>
        public static int Count => Surfaces.Count;

        /// <summary>The live registry, for a consumer that wants to drive a pure overload with it (and for
        /// diagnostics). Read-only: add and remove through <see cref="Register"/> /
        /// <see cref="Unregister"/> so the two never diverge.</summary>
        public static IReadOnlyList<IStandableSurface> Active => Surfaces;

        /// <summary>
        /// Add a surface to the registry (a registrant's <c>OnEnable</c>). Null and double registration are
        /// no-ops: a component that is enabled twice without a disable between must not end up in the list
        /// twice, or its deck would be tested twice for the same answer.
        /// </summary>
        public static void Register(IStandableSurface surface)
        {
            if (surface == null || Surfaces.Contains(surface)) return;
            Surfaces.Add(surface);
        }

        /// <summary>Remove a surface from the registry (a registrant's <c>OnDisable</c>). A surface that was
        /// never registered is a no-op — a teardown must never have to check first.</summary>
        public static void Unregister(IStandableSurface surface)
        {
            if (surface == null) return;
            Surfaces.Remove(surface);
        }

        /// <summary>Empty the registry (scene teardown / test isolation). A leaked registration would make
        /// unrelated water walkable, so tests clear this in their fixture.</summary>
        public static void Clear() => Surfaces.Clear();

        // ---- the lookup -----------------------------------------------------------------------

        /// <summary>
        /// The <b>highest</b> registered deck over <paramref name="worldPos"/>, if any. Highest wins because
        /// overlapping structures stack physically (M2: a gangway lying on a wharf; a washboard over a
        /// deck) and you stand on the top one. A null or empty list is "no structures" → <c>false</c>.
        /// Pure: the caller supplies the surfaces, so tests need no registry.
        /// </summary>
        public static bool TryGetDeckElevation(IReadOnlyList<IStandableSurface> surfaces, Vector2 worldPos,
                                               out float deckElevation)
        {
            deckElevation = 0f;
            if (surfaces == null) return false;

            bool found = false;
            for (int i = 0; i < surfaces.Count; i++)
            {
                IStandableSurface s = surfaces[i];
                if (s == null) continue;
                if (!s.TryGetDeckElevation(worldPos, out float deck)) continue;
                if (!found || deck > deckElevation) { deckElevation = deck; found = true; }
            }
            return found;
        }

        /// <summary>The highest deck over a position among the LIVE registrants (the <c>Now</c> twin of
        /// <see cref="TryGetDeckElevation(IReadOnlyList{IStandableSurface},Vector2,out float)"/>).</summary>
        public static bool TryGetDeckElevationNow(Vector2 worldPos, out float deckElevation)
            => TryGetDeckElevation(Surfaces, worldPos, out deckElevation);

        // ---- the composition ------------------------------------------------------------------

        /// <summary>
        /// The <b>on-foot standing elevation</b> (m above chart datum) at a position: the deck of the
        /// highest registered surface over it, or <paramref name="groundElevation"/> where there is none.
        /// This is the whole seam — one number, substituted for the ground, feeding the existing
        /// <see cref="TidalExposure"/> maths unchanged.
        ///
        /// <para>A surface WINS over the ground even when the ground is higher: a deck you are standing on
        /// is where your feet are, whatever the hill beside it does. (For St Peters that never bites — the
        /// pier's deck is level with the last dry ground it launches from — but a jetty cut into a bank
        /// must not silently inherit the bank's height.)</para>
        /// </summary>
        public static float StandingElevation(float groundElevation, IReadOnlyList<IStandableSurface> surfaces,
                                              Vector2 worldPos)
            => TryGetDeckElevation(surfaces, worldPos, out float deck) ? deck : groundElevation;

        /// <summary>The <c>Now</c> twin of <see cref="StandingElevation"/>, over the live registry.</summary>
        public static float StandingElevationNow(float groundElevation, Vector2 worldPos)
            => StandingElevation(groundElevation, Surfaces, worldPos);

        /// <summary>
        /// The <b>ON-FOOT water depth</b> (m) over a position — the single composition every on-foot
        /// consumer reads (the walk gate, the sprint gate, the wade bands, the body's waterline), so they
        /// cannot disagree about where the fisher's feet are. ≤ 0 is dry; &gt; 0 is metres of water over the
        /// standing surface.
        ///
        /// <para>Composes the deterministic water level
        /// (<see cref="IEnvironmentService.WaterLevelAt"/>, recomputed from <c>(worldSeed, gameTime)</c>)
        /// with the <see cref="StandingElevation"/> at the position. With no
        /// <paramref name="surfaces"/> this is byte-for-byte the terrain-vs-water read that predates the
        /// seam.</para>
        ///
        /// <para>Returns <see cref="float.NegativeInfinity"/> ("as dry as can be") when either service is
        /// absent: the region simply isn't tide-gated, so the gate is off rather than trapping the walker.
        /// This is the established gate-off contract — do not change it to 0.</para>
        /// </summary>
        public static float OnFootDepth(ITidalTerrain terrain, IEnvironmentService environment,
                                        IReadOnlyList<IStandableSurface> surfaces,
                                        double totalSeconds, Vector2 worldPos)
        {
            if (terrain == null || environment == null) return float.NegativeInfinity;
            float standing = StandingElevation(terrain.ElevationAt(worldPos), surfaces, worldPos);
            return TidalExposure.WaterDepth(environment.WaterLevelAt(totalSeconds), standing);
        }

        /// <summary>
        /// The <c>Now</c> twin of <see cref="OnFootDepth"/>: the live on-foot depth over a position, read
        /// off the Core services (<see cref="GameServices.TidalTerrain"/> /
        /// <see cref="GameServices.Environment"/> at the current <see cref="IGameClock.TotalSeconds"/>) and
        /// the live surface registry. The one entry point runtime consumers call.
        /// </summary>
        public static float OnFootDepthNow(Vector2 worldPos)
        {
            IGameClock clock = GameServices.Clock;
            double now = clock != null ? clock.TotalSeconds : 0.0;
            return OnFootDepth(GameServices.TidalTerrain, GameServices.Environment, Surfaces, now, worldPos);
        }
    }
}
