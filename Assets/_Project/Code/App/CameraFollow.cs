using UnityEngine;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Greybox camera that smoothly follows a target (the dory) so it stays on screen. A proper
    /// camera rig (look-ahead, bounds, zoom by boat size) is a later ui-ux/world task; this is just
    /// enough to play.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 4f;

        private void LateUpdate()
        {
            if (Target == null) return;
            Vector3 goal = Target.position;
            goal.z = transform.position.z;           // keep the camera's depth
            transform.position = Vector3.Lerp(transform.position, goal,
                                              1f - Mathf.Exp(-Smooth * Time.deltaTime));
        }
    }
}
