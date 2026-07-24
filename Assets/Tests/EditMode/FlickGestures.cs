using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Shared test driver for the FLICK-CAST gesture (Rod Fishing v2 §2.2): the press-to-cast is retired,
    /// so any test that needs a line in the water performs the real gesture — press with a pointer behind
    /// the character, wind back, sweep forward past them, release near the sweet point, then tick the
    /// line's flight to touchdown. Used by the controller, licence-gate and dev-input tests so they all
    /// cast through the one real path.
    /// </summary>
    internal static class FlickGestures
    {
        /// <summary>Perform a clean, well-timed flick on the controller (press → wind back → sweep →
        /// release). Leaves the controller in the Cast phase (line in flight) — or Idle if the cast was
        /// refused (e.g. a full hold never starts the wind-back).</summary>
        public static void Flick(FishingController c)
        {
            Vector2 a = c.transform.position;
            c.Tick(0.02f, true,  a + new Vector2(0f, -0.5f), true);   // press: the wind-back starts
            c.Tick(0.02f, true,  a + new Vector2(0f, -1.0f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f, -1.5f), true);   // the wind-back apex
            c.Tick(0.02f, true,  a + new Vector2(0f, -0.5f), true);   // the forward sweep…
            c.Tick(0.02f, true,  a + new Vector2(0f,  0.3f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f,  1.0f), true);   // …snapped briskly through
            c.Tick(0.02f, false, a + new Vector2(0f,  1.0f), true);   // release → the line flies
        }

        /// <summary>Flick, then tick the line's flight down to touchdown — the controller ends in
        /// Waiting (a line in the water), the state every pre-flick test used to reach with one press.</summary>
        public static void CastLine(FishingController c)
        {
            Flick(c);
            float t = 0f;
            while (c.Phase == FishingPhase.Cast && t < 5f) { c.Tick(0.05f, false); t += 0.05f; }
        }

        /// <summary>The same clean flick, driven through DevFishingInput's public tick (the gate/latch
        /// path) — leaves the rig in Cast, or Idle wherever the gate refused the gesture.</summary>
        public static void Flick(DevFishingInput dev, FishingController c)
        {
            Vector2 a = c.transform.position;
            dev.TickFishing(0.02f, true,  a + new Vector2(0f, -0.5f), true);
            dev.TickFishing(0.02f, true,  a + new Vector2(0f, -1.0f), true);
            dev.TickFishing(0.02f, true,  a + new Vector2(0f, -1.5f), true);
            dev.TickFishing(0.02f, true,  a + new Vector2(0f, -0.5f), true);
            dev.TickFishing(0.02f, true,  a + new Vector2(0f,  0.3f), true);
            dev.TickFishing(0.02f, true,  a + new Vector2(0f,  1.0f), true);
            dev.TickFishing(0.02f, false, a + new Vector2(0f,  1.0f), true);
        }

        /// <summary>Flick through the dev input, then fly the line to touchdown (Waiting) — or wherever
        /// the gate left the FSM if the gesture was refused.</summary>
        public static void CastLine(DevFishingInput dev, FishingController c)
        {
            Flick(dev, c);
            float t = 0f;
            while (c.Phase == FishingPhase.Cast && t < 5f) { dev.TickFishing(0.05f, false); t += 0.05f; }
        }
    }
}
