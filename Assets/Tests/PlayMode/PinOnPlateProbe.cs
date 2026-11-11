#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Vehicles;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>Reads the pin where the player sees it — after the hitch has followed</b>, and, while it
    /// reads, deals the frames out unevenly on purpose.
    ///
    /// <para><b>Why a probe and not a line in the test's loop.</b> <see cref="VehicleHitch"/> places her
    /// trailer in <c>LateUpdate</c>, from the tractor's post-physics pose. A coroutine can only resume
    /// before that: after <see cref="WaitForFixedUpdate"/>, or after the next frame's <c>Update</c>
    /// (<c>yield return null</c>). Read there, the pin is off the plate by however far the tractor drove
    /// in that frame's physics steps — nothing on a fast machine, whose next frame steps nothing, but
    /// ~0.08 m per step at tow speed when it does (measured 11-11: 0.081 for one step, 0.159 for two).
    /// That window was what <c>RoadFleetJourneyPlayTests</c> measured until 11-11, so a runner whose
    /// frames each held a step read 0.051–0.068 m against the 0.05 m bar, while the pin the camera drew
    /// sat 8 µm from the plate. This runs its <c>LateUpdate</c> after every vehicle script (order 10000;
    /// the hitch has none, the mesh driver is −110), the last thing before the frame is drawn.</para>
    ///
    /// <para><b>The cadence is the guard.</b> While armed it sets <see cref="Time.captureDeltaTime"/>
    /// for the next frame from a fixed cycle of whole and part physics steps, so the tow runs frames of
    /// 0, 1, 2 and 3 steps on every machine, CI included — the long frames that used to arrive by luck
    /// arrive by schedule. <see cref="MostStepsInAFrame"/> proves they did.</para>
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class PinOnPlateProbe : MonoBehaviour
    {
        /// <summary>The frame cycle, in physics steps per frame. Sums to 10 over 8 frames, holds a
        /// 3-step frame and two frames that step nothing.</summary>
        private static readonly float[] StepsPerFrame = { 1f, 0.5f, 3f, 1f, 0.25f, 2f, 1.5f, 0.75f };

        private VehicleHitch _hitch;
        private TowedBody _trailer;
        private Vector3 _pinLocal;
        private float _captureBefore;
        private int _frame;
        private int _stepsThisFrame;

        public float WorstPinOffPlate { get; private set; }
        public float WorstDrawnPinOffPlate { get; private set; }
        public int FramesSampled { get; private set; }
        public int MostStepsInAFrame { get; private set; }
        public bool Armed => _hitch != null;

        public void Arm(VehicleHitch hitch, TowedBody trailer)
        {
            _hitch = hitch;
            _trailer = trailer;
            _pinLocal = new Vector3(trailer.Kingpin.CouplingPointLocal.x,
                                    trailer.Kingpin.CouplingPointLocal.y, 0f);
            _captureBefore = Time.captureDeltaTime;
            _frame = 0;
            _stepsThisFrame = 0;
            WorstPinOffPlate = WorstDrawnPinOffPlate = 0f;
            FramesSampled = MostStepsInAFrame = 0;
            Time.captureDeltaTime = StepsPerFrame[0] * Time.fixedDeltaTime;
        }

        public void Disarm()
        {
            if (!Armed) return;
            _hitch = null;
            _trailer = null;
            Time.captureDeltaTime = _captureBefore;
        }

        private void OnDestroy() => Disarm();

        private void FixedUpdate()
        {
            if (Armed) _stepsThisFrame++;
        }

        private void LateUpdate()
        {
            if (!Armed || _trailer == null) return;

            Vector2 plate = _hitch.CouplingPointWorld;
            WorstPinOffPlate = Mathf.Max(WorstPinOffPlate, Vector2.Distance(_trailer.KingpinWorld, plate));
            WorstDrawnPinOffPlate = Mathf.Max(WorstDrawnPinOffPlate,
                Vector2.Distance(_trailer.transform.TransformPoint(_pinLocal), plate));
            MostStepsInAFrame = Mathf.Max(MostStepsInAFrame, _stepsThisFrame);
            _stepsThisFrame = 0;
            FramesSampled++;

            _frame++;
            Time.captureDeltaTime = StepsPerFrame[_frame % StepsPerFrame.Length] * Time.fixedDeltaTime;
        }
    }
}
#endif
