using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>Puts the fisher at the wheel</b> — the rig-6.5 <c>drive</c> clip, sat on the machine's own
    /// seat, facing wherever she is pointing.
    ///
    /// <para><b>The driver did not exist.</b> ADR 0035 shipped the drive mode with the player HIDDEN:
    /// <c>ApplyPlayerFor</c> gave <see cref="ControlMode.Driving"/> the helm's frozen, hidden,
    /// un-simulated shape, and <c>RideVehicle</c>'s own comment says the seating is "about where the
    /// CAMERA is looking, not about a figure anyone can see". That was the right call for the machine it
    /// was written against — the Dually's crew cab is a room with a roof panel and glass that is opaque
    /// at 32 px/m — and it is the wrong call for an open one. This draws the driver, and the machine
    /// decides which she is.</para>
    ///
    /// <para><b>THE ART DECIDES, NOT A RULE HERE</b> (the same shape <c>VehicleController</c> uses to
    /// decide a drivetrain). A machine whose rig publishes a seat in the OPEN answers
    /// <see cref="IDriveSeat.ShowsDriver"/> and gets a driver; one whose seats are inside a cab publishes
    /// none and keeps hers hidden, byte-for-byte as she shipped. Nothing here names a vehicle, and the
    /// day a hard-cab amphibian arrives this file does not learn about her.</para>
    ///
    /// <para><b>⚠️ THE FACING IS THE MACHINE'S, RE-DERIVED EVERY FRAME</b> — the law on the clip's own
    /// doc, and the one thing about a driver that is easy to get wrong. A driver faces where the machine
    /// points, so the heading a player happened to walk up at is not it, and the heading they boarded at
    /// is not it either: cache either and the fisher stares off across the park for the whole of the
    /// first turn. It is read off her root every tick (<c>transform.up</c> is the nose, the fleet's one
    /// convention) through the very function her own picture is drawn from
    /// (<see cref="BoatKinematics.BearingDegrees"/>), so the driver and the machine cannot disagree —
    /// including the day the boat lane's bearing is un-squashed (ADR 0034's open point), because both
    /// sides read the same call.</para>
    ///
    /// <para><b>Nothing is read off the CONTROLS.</b> The pose does not consult throttle, brake or wheel,
    /// and that is deliberate rather than unfinished: the rig bakes one looping drive cycle and no lean,
    /// and the two machines in the game steer by different means — the Otter is skid-steered and
    /// publishes no steer axis at all, the Dually is Ackermann. A pose that guessed a lean from velocity
    /// would be inventing a fact one of them does not have. If a driver ever leans into a turn it reads
    /// the machine's PUBLISHED axes, never a guess.</para>
    ///
    /// <para><b>Inputs early, picture late</b> — <c>DeckRiderVisual</c>'s lesson, which cost a PlayMode
    /// test to find. The heading is stated in <c>Update</c>, because
    /// <see cref="CharacterClipPlayer"/> chooses its cell in <c>LateUpdate</c> at execution order 0 and a
    /// heading written after that would draw the driver a whole facing bucket behind her machine on every
    /// turn. The SEATING is written in <c>LateUpdate</c> at 100, because <c>ControlSwitcher</c> seats the
    /// driver on the machine's root in ITS LateUpdate at 0 and this is the offset from there.</para>
    ///
    /// <para><b>Why the switcher hands this the seat rather than this subscribing to a signal.</b> Two
    /// facts have to be decided in one breath — whether the driver is drawn at all, and where she sits —
    /// and only this component can answer the first, because it alone knows whether the CLIP has art.
    /// <c>ApplyPlayerFor</c> is the funnel every transition already goes through (entry, exit, a truck
    /// dying under the driver, a region hop's re-assert) and it is where "who draws the character" is
    /// already decided for every other mode. Answering it from there keeps ONE owner of the renderer's
    /// <c>enabled</c> flag; a presenter that armed itself off a signal would be a second, and the two
    /// would fight over it every frame.</para>
    ///
    /// <para><b>Degrades whole.</b> No clip player, no drive art, no mounts asset, or a machine that
    /// publishes no open seat → <see cref="SetSeat"/> answers false, the switcher keeps hiding the
    /// player, and the drive mode is exactly what ADR 0035 shipped. Every one of those is answered in the
    /// same breath as the visibility, so there is no state in which a fisher is drawn STANDING on a
    /// truck — the picture is either the whole driver or the shipped nobody.</para>
    /// </summary>
    [DisallowMultipleComponent]
    // The same slot DeckRiderVisual takes, and for the same reason: this writes the seating AFTER
    // ControlSwitcher (order 0) has put the driver on the machine's root, and after the clip player
    // (order 0) has chosen the cell the heading stated in Update asked for.
    [DefaultExecutionOrder(100)]
    public sealed class PlayerDrivePresenter : MonoBehaviour
    {
        [Header("Wiring (auto-resolved from this GameObject when left empty)")]
        [Tooltip("The shared clip seam. It claims the renderer from the iso skin itself, through the " +
                 "same counted Suspend/Release the deck haul uses, so the two can never fight.")]
        [SerializeField] private CharacterClipPlayer _clipPlayer;

        [Tooltip("The rig-6.5 off-deck mount contract. Only ONE number is read from it — DriveSeatZ, " +
                 "the seat the drive sheets were baked sitting on — and it is read rather than typed " +
                 "here so a re-bake moves the driver without a code change (rule 6).")]
        [SerializeField] private CharacterOffDeckMountsDef _mounts;

        private IDriveSeat _seat;
        private bool _showing;

        /// <summary>True while the driver is on screen at a wheel. Public so a test can watch her appear
        /// and disappear without a screen grab.</summary>
        public bool IsShowing => _showing;

        /// <summary>The machine she is being drawn on, or null. For tests and tooling.</summary>
        public IDriveSeat Seat => _seat;

        /// <summary>Wire the mount contract explicitly (the builder's and the tests' path).</summary>
        public void ConfigureMounts(CharacterOffDeckMountsDef mounts) => _mounts = mounts;

        /// <summary>Wire the clip seam explicitly (tests). Null hands it back to the auto-resolve.</summary>
        public void ConfigureClipPlayer(CharacterClipPlayer clipPlayer) => _clipPlayer = clipPlayer;

        /// <summary>
        /// The clip seam this plays through — the wired one, else the one beside it.
        ///
        /// <para>⚠️ <b>Resolved on FIRST USE, not in <c>Awake</c>, and that is a correctness fix rather
        /// than a convenience.</b> <c>Awake</c> does not run on a component in EDIT MODE, and this
        /// presenter is reachable there: <c>ControlSwitcher.TryEnterDriving</c> is public and EditMode
        /// suites drive it directly, so an <c>Awake</c>-only reference would leave this answering "no
        /// driver" at edit time while answering yes in a build — the same fact, decided two ways by which
        /// lifecycle happened to have run. The same lazy shape <c>ControlSwitcher.DeckRider</c> and
        /// <c>CharacterClipPlayer.ResolvedVisual</c> already use, and it self-caches, so the hot path
        /// costs one null test.</para>
        /// </summary>
        private CharacterClipPlayer ClipPlayer
        {
            get
            {
                if (_clipPlayer == null) _clipPlayer = GetComponent<CharacterClipPlayer>();
                return _clipPlayer;
            }
        }

        /// <summary>
        /// <b>Would a driver be drawn on this machine?</b> Every reason there is for "no", asked without
        /// changing anything — the question <c>ControlSwitcher.ApplyPlayerFor</c> puts before it decides
        /// whether the player's renderer is on.
        ///
        /// <para>Every gate is real and none is redundant: a machine that models no visible driver, a seat
        /// that has died between the press and here, a skin whose drive sheets are not baked (clip art is
        /// all-or-nothing — <see cref="CharacterClipPlayer.CanPlay"/>), a mount contract that is not
        /// wired, and this component switched off. Two are worth naming. Without <c>DriveSeatZ</c> the
        /// seat the pose was baked on is unknown, and drawing her anyway would put the fisher a whole
        /// 0.40 m — a dozen pixels — above the cushion. And a DISABLED presenter gets no <c>Update</c> or
        /// <c>LateUpdate</c>, so a yes from one would be a driver frozen on frame 0 at the machine's own
        /// centre, which is worse than the hidden driver it replaced.</para>
        /// </summary>
        public bool WouldShow(IDriveSeat seat)
        {
            if (!isActiveAndEnabled) return false;
            if (seat == null || !seat.IsAlive || seat.Root == null) return false;
            if (!seat.ShowsDriver) return false;
            if (_mounts == null) return false;
            CharacterClipPlayer clip = ClipPlayer;
            return clip != null && clip.CanPlay(CharacterClip.Drive);
        }

        /// <summary>
        /// <b>Hand over the machine the player is sitting in</b> — or null for "not driving" — and get
        /// back whether her driver is actually being drawn on it.
        ///
        /// <para>Idempotent on the same machine, which matters: a region hop re-asserts the drive mode
        /// through the same path, and re-playing a looping clip every time would freeze the driver on
        /// frame 0 forever.</para>
        /// </summary>
        public bool SetSeat(IDriveSeat seat)
        {
            if (!WouldShow(seat)) { Stop(); return false; }
            if (_showing && ReferenceEquals(_seat, seat)) return true;   // same machine — leave the clip running

            Stop();                                   // a DIFFERENT machine: start her cleanly on that one
            _seat = seat;

            if (!ClipPlayer.Play(CharacterClip.Drive, HeadingDegrees()))
            {
                _seat = null;                         // refused: change nothing, and stay hidden
                return false;
            }

            _showing = true;
            SeatDriver();                                  // seated on the first frame, not the second
            return true;
        }

        // Torn down clean: a presenter switched off mid-drive hands the CLIP back rather than leaving the
        // fisher stuck on a driving frame. Visibility is not this component's to put back — the switcher
        // decided it and re-decides it at the next transition, and grabbing the flag here to "tidy up"
        // would make this the second owner of it, which is the one thing the arrangement above avoids.
        private void OnDisable() => Stop();

        /// <summary>State the facing BEFORE the clip player picks its cell (execution order 0 runs its
        /// LateUpdate before this component's). Physics has already run for the frame, so the machine's
        /// heading here is the same one her own picture is being drawn at.</summary>
        private void Update()
        {
            if (!_showing) return;
            if (!Alive()) { Stop(); return; }
            ClipPlayer.SetHeading(HeadingDegrees());
        }

        /// <summary>Seat her, AFTER <c>ControlSwitcher.LateUpdate</c> has put the driver on the machine's
        /// root. This is the offset from that point to the cushion.</summary>
        private void LateUpdate()
        {
            if (!_showing) return;
            if (!Alive()) { Stop(); return; }
            SeatDriver();
        }

        /// <summary>Where the machine is pointing, as the compass bearing her own renderer is drawn from
        /// (<c>transform.up</c> is the nose). Read live, never cached — see the class doc.</summary>
        private float HeadingDegrees() => BoatKinematics.BearingDegrees(_seat.Root.up);

        /// <summary>Put the drive cell's pivot on the machine's seat. The character's own z is carried
        /// across untouched — the iso sort band lives there.</summary>
        private void SeatDriver()
        {
            transform.position = DriveSeatMath.SeatPivotWorld(
                _seat.DriverSeatWorldPosition, _seat.DriverSeatHeightMeters,
                _mounts.DriveSeatZ, transform.position.z);
        }

        /// <summary>Is the machine still really there? Through <see cref="IDriveSeat.IsAlive"/>, never a
        /// plain null test — the reference here is INTERFACE-typed, so <c>==</c> does not see Unity's
        /// fake-null and a destroyed truck reads as a live seat right up until the next dereference
        /// throws. The switcher notices the same death in its own Update and hands control back; this
        /// guard is what keeps the one frame in between from being an exception.</summary>
        private bool Alive() => _seat != null && _seat.IsAlive && _seat.Root != null;

        /// <summary>Hand the renderer back and forget the machine (idempotent).</summary>
        private void Stop()
        {
            _seat = null;
            if (!_showing) return;
            _showing = false;

            // Only our own clip. Something else may have claimed the renderer since, and stopping its clip
            // would be a visible glitch on a picture this no longer owns.
            if (_clipPlayer != null && _clipPlayer.Clip == CharacterClip.Drive) _clipPlayer.Stop();
        }
    }
}
