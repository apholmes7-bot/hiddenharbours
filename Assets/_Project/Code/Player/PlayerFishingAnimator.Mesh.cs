using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    public sealed partial class PlayerFishingAnimator
    {
        private float _digElapsed = -1f;
        private float _digHeading;
        private ActionTiming _digTimingValue;

        private static bool MeshPerformanceOn =>
            GameServices.Config != null && GameServices.Config.MeshFishingAndDig;

        /// <summary>The sprite performance remains live underneath the mesh, ready for any fallback.</summary>
        public bool HasBodyPerformance => isActiveAndEnabled &&
            (_active || (MeshPerformanceOn && _digElapsed >= 0f));

        /// <summary>The dig's existing success cue, emitted by both the in-hand and pail paths.
        /// Presentation only: yield, transport, save and input timing are untouched.</summary>
        public void OnDigStrike(JuiceMomentCue cue)
        {
            if (cue.Kind != JuiceMoment.DigStrike || !MeshPerformanceOn || !isActiveAndEnabled) return;
            ResolveIsoSkin();
            if (_isoSkin == null) return;
            var timing = Resources.Load<ActionTimingDef>(LiftFeedback.DigTimingResource);
            if (timing == null || timing.Timing.TotalSeconds <= 0f) return;
            _digTimingValue = timing.Timing;
            _digElapsed = 0f;
            Vector2 offset = cue.At - (Vector2)transform.position;
            _digHeading = offset.sqrMagnitude >= MinFacingOffsetM * MinFacingOffsetM
                ? IsoGround.BearingDegrees(offset) : _isoSkin.HeadingDegrees;
        }

        private void AdvanceDig(float dt)
        {
            if (_digElapsed < 0f) return;
            _digElapsed += Mathf.Max(0f, dt);
            if (!MeshPerformanceOn || ActionTimingMath.IsDone(_digElapsed, in _digTimingValue))
                _digElapsed = -1f;
        }

        /// <summary>Use the sprite animator's exact pose, clock, cast charge and facing. The skin's
        /// own frame count is used with the SAME timing function, including the big-fish strike hold.
        /// Only our own suspension claim may be bypassed. Every refusal leaves the sprite intact.</summary>
        public bool TryMeshPose(CharacterSkinDef skin, out string key, out int frame, out float heading)
        {
            key = null; frame = 0; heading = 0f;
            if (!HasBodyPerformance || !MeshPerformanceOn || skin == null || !skin.IsUsable()) return false;
            ResolveIsoSkin();
            if (_isoSkin == null) return false;
            if (_active)
            {
                if (_isoSkin.SuspensionCount != 1) return false;
                key = PlayerFishingAnimMath.StateKeyFor(_pose);
                heading = (_rowsAreCounterClockwise ? -1f : 1f) * _row * (360f / Directions);
            }
            else
            {
                if (_isoSkin.IsSuspended) return false;
                key = PlayerFishingAnimMath.DigStateKey;
                heading = _digHeading;
            }
            if (key == null || !skin.DrawsAsMesh(key) || !skin.TryGetClip(key, out var clip) ||
                !clip.KeysWellFormed(skin.BoneCount)) return false;
            frame = _active ? FrameFor(_pose, clip.FrameCount)
                : ActionTimingMath.FrameFor(_digElapsed, in _digTimingValue, clip.FrameCount);
            return true;
        }
    }
}
