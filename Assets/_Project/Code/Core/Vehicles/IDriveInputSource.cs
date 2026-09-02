namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One frame of a driver's demand</b> — what a wheel, two pedals and a brake ask of a machine,
    /// as numbers and nothing else.
    ///
    /// <para><see cref="Throttle"/> is −1 (full astern) … +1 (full ahead). <see cref="Steer"/> is −1
    /// (full right) … +1 (full LEFT) — the rig's own steering sense (<c>+1 = full LEFT lock</c>), so
    /// the A key, the drawn wheels and the yaw agree with no sign flip hidden anywhere.
    /// <see cref="Brake"/> is the one control a driver expects to be separate from a negative throttle
    /// (which is REVERSE, and stays reverse). The wheel POSITION is the machine's business: it moves
    /// toward this at her own steering rate, so the picture and the yaw stay solved from one number
    /// (ADR 0035 §5).</para>
    /// </summary>
    public readonly struct DriveDemand
    {
        public readonly float Throttle;
        public readonly float Steer;
        public readonly bool Brake;

        public DriveDemand(float throttle, float steer, bool brake)
        {
            Throttle = throttle;
            Steer = steer;
            Brake = brake;
        }

        /// <summary>Nothing asked — throttle shut, wheel released, brake off. What a source with no
        /// device behind it answers, and what a machine coasts on.</summary>
        public static DriveDemand None => default;
    }

    /// <summary>
    /// ⭐ <b>Where a driver's demand comes from</b> — the ONE seam between whatever is being driven and
    /// whatever is driving it (ADR 0035, amended 2026-09-02).
    ///
    /// <para><b>Why a seam and not a keyboard read.</b> The control mode used to read
    /// <c>Keyboard.current</c> inline, every frame a machine was being driven — and with no key held,
    /// which is every frame of a headless run, that read landed a zero demand and overwrote anything a
    /// fixture had asked for. A PlayMode journey that set full throttle and stepped thirty seconds of
    /// physics measured <b>0.00 m</b>, and its failure pointed at the yard rather than at the input
    /// path (memory <c>driveinput-is-zeroed-by-the-keyboard-read</c>). Handing the read to a source
    /// retires that trap where it lives: the switcher asks the source, and a source that HOLDS a demand
    /// holds it across frames.</para>
    ///
    /// <para><b>It is also the socket.</b> The shipped source is the greybox keyboard, byte for byte the
    /// read it replaces; a gamepad, a replay, an NPC at a wheel, or a scripted test driver is another
    /// implementation of this and nothing else changes. ⚠️ Not an intent layer: the walk and the helm
    /// read raw keys too, and unifying the three is a project-wide input lane
    /// (<c>docs/design/ux-and-mobile-controls.md</c> §9), not a vehicle change. One seam, one keyboard
    /// source, one held source.</para>
    ///
    /// <para><b>Contract.</b> <see cref="Read"/> is polled once per frame while — and only while — a
    /// machine is being driven, and answers the demand for THAT frame. The switcher hands every answer
    /// straight to the seat; a source is never told a frame was skipped and must not remember one.</para>
    /// </summary>
    public interface IDriveInputSource
    {
        DriveDemand Read();
    }

    /// <summary>
    /// <b>A demand held until it is changed</b> — the scripted source.
    ///
    /// <para>What a headless journey drives a machine with: set a throttle and it is still set on the
    /// next frame, the next physics step, and the thirty seconds after. Also what a replay or an NPC
    /// driver would hand the switcher — anything that decides a demand somewhere other than in the
    /// frame it is read.</para>
    ///
    /// <para><see cref="Reads"/> counts how many frames a driver actually asked. It is the anti-vacuous
    /// number: a test proving the seam carries a demand must also prove the seam was consulted, and a
    /// test proving that a demand nobody is seated for moves nothing must see it was NOT.</para>
    /// </summary>
    public sealed class HeldDriveInput : IDriveInputSource
    {
        private DriveDemand _demand;

        /// <summary>How many frames the demand has been read — see the class note.</summary>
        public int Reads { get; private set; }

        /// <summary>What is being asked right now.</summary>
        public DriveDemand Demand => _demand;

        public void Set(float throttle, float steer, bool brake) =>
            _demand = new DriveDemand(throttle, steer, brake);

        public void Set(in DriveDemand demand) => _demand = demand;

        /// <summary>Let go of everything — throttle shut, wheel released, brake off.</summary>
        public void Release() => _demand = DriveDemand.None;

        public DriveDemand Read()
        {
            Reads++;
            return _demand;
        }
    }
}
