using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// DEV/DEMO ONLY — a throwaway WASD/arrow mover so the owner can walk the demo avatar through the grass test
    /// patch (Hidden Harbours ▸ Build Grass Test) and watch the footstep bend, WITHOUT the real player controller
    /// (which lives in the gameplay-systems lane). It moves a plain transform; no physics. Uses the new Input
    /// System (<see cref="Keyboard.current"/>), matching this project's input setting. The real on-foot player
    /// will carry <see cref="GrassFootstep"/> instead; this just stands in for it in the demo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrassDevWalker : MonoBehaviour
    {
        [Tooltip("Move speed (m/s).")]
        [Min(0f)] [SerializeField] private float _speed = 3.5f;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float x = 0f, y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    y += 1f;

            var dir = new Vector2(x, y);
            if (dir.sqrMagnitude > 1e-4f)
                transform.position += (Vector3)(dir.normalized * (_speed * Time.deltaTime));
        }
    }
}
