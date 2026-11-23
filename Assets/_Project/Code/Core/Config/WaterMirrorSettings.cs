using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The calm mirror's shaping dials (the owner, 2026-11-23: calm water must show a TRUE reflection, at
    /// the prototype's strength). The strength itself is <see cref="GameConfig.WaterMirrorStrength"/>;
    /// at 0 none of these is read and the water draws as it did before the mirror existed.
    ///
    /// <para>Presentation only. Nothing in the simulation, the save or a boat's motion reads them.</para>
    /// </summary>
    [Serializable]
    public struct WaterMirrorSettings
    {
        [Header("Sea state")]
        [Tooltip("Sea state (0..1) at which the mirror starts to fade. Below this it is at full strength.")]
        [Range(0f, 1f)] public float SeaFadeStart;
        [Tooltip("Sea state (0..1) at which the mirror is gone; the water's own object reflection has it back.")]
        [Range(0f, 1f)] public float SeaFadeEnd;

        [Header("Reach")]
        [Tooltip("Tallest thing the flip mirrors, metres above its base on the water. Bounds the per-pixel search " +
                 "(never past ReachEndMetres, where the flip is gone).")]
        [Range(1f, 40f)] public float MaxMetres;
        [Tooltip("Reflected distance, metres below the base, at which the flipped picture starts to thin.")]
        [Range(0f, 40f)] public float ReachStartMetres;
        [Tooltip("Reflected distance, metres below the base, at which the flipped picture is gone.")]
        [Range(0f, 40f)] public float ReachEndMetres;
        [Tooltip("Art pixels between the search's taps up the screen; it refines to the exact pixel after a hit.")]
        [Range(1f, 8f)] public float StepPixels;
        [Tooltip("A base must stand at least this many art pixels tall, or the flip skips it. Keeps thin " +
                 "rings, splashes and lines on the water out of the mirror.")]
        [Range(0f, 8f)] public float MinStandPixels;

        [Header("Ripple")]
        [Tooltip("Sideways ripple of the flipped picture on calm water, metres.")]
        [Range(0f, 1f)] public float RippleCalmMetres;
        [Tooltip("Sideways ripple of the flipped picture as the sea reaches SeaFadeEnd, metres.")]
        [Range(0f, 2f)] public float RippleRoughMetres;
        [Tooltip("Rows of art pixels that ripple together. Larger = calmer, steadier bands; 1 = every row " +
                 "on its own, which reads as zig-zags on tall striped faces such as cliffs.")]
        [Range(1f, 16f)] public float RippleBandPixels;
        [Tooltip("Share of the water that breaks the flipped picture back to the water's own colour on calm water.")]
        [Range(0f, 1f)] public float BreakCalm;
        [Tooltip("The same share as the sea reaches SeaFadeEnd.")]
        [Range(0f, 1f)] public float BreakRough;

        [Header("Light")]
        [Tooltip("How much of the light a reflection returns (a reflection is darker than what it shows).")]
        [Range(0f, 1f)] public float Darken;

        public static WaterMirrorSettings Default => new WaterMirrorSettings
        {
            SeaFadeStart = 0.15f,
            SeaFadeEnd = 0.75f,
            MaxMetres = 12f,
            ReachStartMetres = 3f,
            ReachEndMetres = 10f,
            StepPixels = 3f,
            MinStandPixels = 2f,
            RippleCalmMetres = 0.05f,
            RippleRoughMetres = 0.45f,
            RippleBandPixels = 3f,
            BreakCalm = 0.15f,
            BreakRough = 0.8f,
            Darken = 0.82f,
        };
    }
}
