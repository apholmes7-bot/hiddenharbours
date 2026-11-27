using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What a garment does</b> (store clothing, pass 1). Each kind names ONE system already on main that
    /// reads it through Core; a garment never reaches into a feature module. Append-only, explicit values.
    /// </summary>
    public enum ClothingEffectKind
    {
        /// <summary>Does nothing (an unset row; the content test refuses it).</summary>
        None = 0,
        /// <summary>Eases one need's slow-down and price penalty, exactly as a trait's
        /// <see cref="NeedRelief"/> does (<see cref="NeedsService"/>). The need still rises.</summary>
        NeedRelief = 1,
        /// <summary>Scales the dirt one kind of work adds (<see cref="CleanlinessService"/>): its
        /// per-action bump, its reported chunk, and a stretch of it begun while the garment is worn.</summary>
        DirtScale = 2,
    }

    /// <summary>
    /// One effect on a <see cref="ClothingDef"/>. A plain serializable struct so the owner tunes it in the
    /// inspector and a test builds one in a line. <see cref="Scale"/> is what is LEFT: 0.5 halves the
    /// penalty or the dirt, 1 changes nothing. Effects only ever help (no drawbacks, the trait rule).
    /// </summary>
    [Serializable]
    public struct ClothingEffect
    {
        [Tooltip("Which system this effect moves.")]
        public ClothingEffectKind Kind;

        [Tooltip("NeedRelief only: the need whose penalty this garment eases.")]
        public NeedKind Need;

        [Tooltip("DirtScale only: the kind of work whose dirt this garment keeps off.")]
        public DirtActivity Activity;

        [Tooltip("What is left: 0.5 halves it, 0 cancels it. The content test refuses 1 (no change) and " +
                 "anything outside 0..1 (a drawback).")]
        [Range(0f, 1f)] public float Scale;

        public ClothingEffect(ClothingEffectKind kind, NeedKind need, DirtActivity activity, float scale)
        {
            Kind = kind;
            Need = need;
            Activity = activity;
            Scale = scale;
        }

        /// <summary>A need relief: <paramref name="need"/>'s penalty times <paramref name="scale"/>.</summary>
        public static ClothingEffect Relief(NeedKind need, float scale) =>
            new ClothingEffect(ClothingEffectKind.NeedRelief, need, DirtActivity.None, scale);

        /// <summary>A dirt scale: <paramref name="activity"/>'s dirt times <paramref name="scale"/>.</summary>
        public static ClothingEffect Dirt(DirtActivity activity, float scale) =>
            new ClothingEffect(ClothingEffectKind.DirtScale, NeedKind.Hunger, activity, scale);

        /// <summary><see cref="Scale"/> clamped to 0..1 (NaN reads as 1, no change).</summary>
        public float SafeScale => float.IsNaN(Scale) ? 1f : Scale < 0f ? 0f : Scale > 1f ? 1f : Scale;
    }
}
