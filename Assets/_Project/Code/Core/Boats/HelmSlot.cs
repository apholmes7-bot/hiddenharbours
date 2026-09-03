using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who holds the helm — arbitrated by OCCUPANCY, never by enable order.</b> The one place that
    /// decides which hull's <see cref="IHelmControl"/>/<see cref="IHelmInstruments"/> the game reads
    /// through <see cref="GameServices.HelmControl"/> and <see cref="GameServices.HelmInstruments"/>.
    ///
    /// <para><b>The defect this exists to end</b> (owner playtest 2026-08-21, "it should only show the
    /// boat controls if the player is piloting"). Every <c>BoatController</c> self-installs a helm relay,
    /// and each relay used to ASSIGN itself into the Core slot on enable. Two hulls alive meant the one
    /// enabled LAST owned the helm, wherever the player happened to be standing — so the St Peters
    /// arrival drew the passenger the skipper's wheel and gauges, and any later-spawned hull (ambient
    /// fleet, a purchased boat, a region hop's spawn order) silently took the player's helm away from
    /// them for good: the loser never re-registered.</para>
    ///
    /// <para><b>The shape that fixes it: relays REQUEST, this arbitrates.</b> A relay
    /// <see cref="Register"/>s itself against the hull it rides and nothing more — registering is not
    /// winning. The Player lane separately declares which hull the player is actually piloting
    /// (<see cref="SetPilotedHull"/>, published by <c>ControlSwitcher</c> off its own control mode). The
    /// slot is granted to the registration whose hull IS that hull, and to no other. Order of enabling
    /// cannot change the answer, because enabling is not part of the question.</para>
    ///
    /// <para><b>No fallback, deliberately.</b> Nothing declared piloted ⇒ the slot is EMPTY, even when
    /// exactly one relay is registered. "The only boat in the scene must be the player's" is precisely
    /// the guess that produced the defect — it is right until the day a second hull exists, and then it
    /// is wrong silently. A consumer reading a null slot hides its helm, which is the correct picture
    /// for a player who is not driving anything.</para>
    ///
    /// <para><b>A hull is an opaque identity token.</b> Core never dereferences it — it only ever asks
    /// "is this the same object?". The Boats lane registers with its <c>BoatController</c>; the Player
    /// lane declares that same instance. Matching is by <see cref="object.ReferenceEquals"/> so that
    /// TWO DESTROYED hulls can never match each other (Unity's <c>==</c> reports both as null, which
    /// would make every dead hull equal to every other); liveness is asked separately, through Unity's
    /// overloaded <c>==</c>, which is the only operator that sees a destroyed object as gone
    /// (never <c>??</c>, <c>?.</c> or <c>is null</c> — they compare the raw reference and are satisfied
    /// by a fake-null).</para>
    ///
    /// <para><b>A POCO on purpose.</b> No MonoBehaviour, no Unity lifecycle, no statics of its own — so
    /// the whole arbitration rule is EditMode-testable without a scene, which is where the
    /// order-independence proof belongs (a relay's <c>OnEnable</c> never fires in EditMode).</para>
    /// FLAG lead-architect: new Core contract (the helm-ownership seam).
    /// </summary>
    public sealed class HelmSlot
    {
        private readonly struct Entry
        {
            public readonly IHelmControl Control;
            public readonly IHelmInstruments Instruments;
            public readonly object Hull;

            public Entry(IHelmControl control, IHelmInstruments instruments, object hull)
            {
                Control = control;
                Instruments = instruments;
                Hull = hull;
            }
        }

        // Registered relays, in registration order. Small by construction (one per live hull) and
        // re-used rather than re-allocated, so arbitration costs no garbage (rule 7).
        private readonly List<Entry> _registered = new();

        private object _pilotedHull;
        private object _playersBoat;
        private IHelmControl _control;
        private IHelmInstruments _instruments;

        /// <summary>
        /// The granted piloting seam — the relay of the hull the player is piloting, or null when they
        /// are not piloting one (ashore, on the deck, rowing, watching somebody else steer). This is
        /// what <see cref="GameServices.HelmControl"/> returns.
        /// </summary>
        public IHelmControl Control
        {
            get { if (!GrantStillGood()) Regrant(); return _control; }
        }

        /// <summary>The granted instrument glass — the SAME relay's other face, granted in the same
        /// breath so the dash and the throttle can never describe two different boats. This is what
        /// <see cref="GameServices.HelmInstruments"/> returns.</summary>
        public IHelmInstruments Instruments
        {
            get { if (!GrantStillGood()) Regrant(); return _instruments; }
        }

        /// <summary>The hull the Player lane last declared the player to be piloting (null = nobody).
        /// Exposed so the declarer can check it still owns the declaration before clearing it.</summary>
        public object PilotedHull => _pilotedHull;

        /// <summary>
        /// <b>The boat the player is ON</b> — at the helm OR standing on her deck. Strictly WIDER than
        /// <see cref="PilotedHull"/>, and deliberately a separate fact rather than a synonym:
        ///
        /// <list type="bullet">
        ///   <item>A rowed dory is a boat you are aboard and never a boat you are at the HELM of
        ///   (<c>HasHelm</c> is the engine question), and her ground tackle still has to answer to
        ///   you.</item>
        ///   <item>Stepping back from the wheel onto the deck gives up the helm and does not give up
        ///   the boat. Anchoring is a boat job either way.</item>
        /// </list>
        ///
        /// <para>This is what "is this MY boat?" readers want — the dev anchor key works one hull's
        /// tackle and must not be eaten by a second hull in the scene. They used to ask it by comparing
        /// against the helm slot, which answered it only by accident (every relay took that slot on
        /// enable) and stopped answering it the moment the slot became honest.</para>
        /// </summary>
        public object PlayersBoat => _playersBoat;

        /// <summary>How many hulls are registered right now — every live one, not just the granted
        /// hull. Diagnostic: a test asserting "two hulls, one helm" reads it.</summary>
        public int RegisteredCount => _registered.Count;

        /// <summary>
        /// <b>A relay asking to be considered</b> — never a claim on the slot. Called by the Boats-lane
        /// relay as it enables, naming the hull it rides. Registering the same hull again replaces its
        /// entry: a hull has ONE helm, which is why she is the key rather than the relay.
        /// </summary>
        /// <param name="hull">The hull's identity token — required, and never dereferenced here. A relay
        /// that cannot name a hull cannot be granted one, so it is simply not considered.</param>
        /// <param name="control">Her piloting face (drive, steer, intents); may be null.</param>
        /// <param name="instruments">Her glass face (fit, soundings, prefs); may be null.</param>
        public void Register(object hull, IHelmControl control, IHelmInstruments instruments)
        {
            if (hull == null) return;

            // One hull, one helm — AND one relay, one hull. Drop anything this relay already holds
            // under a DIFFERENT hull before it takes this one. Balanced enable/disable can never
            // produce that state (a relay unregisters the exact token it registered), but if it ever
            // did, the stale entry would be a relay granted for a boat it no longer rides — the same
            // class of wrong answer the whole seam exists to end, and cheap to make unreachable.
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                Entry e = _registered[i];
                if (ReferenceEquals(e.Hull, hull)) continue;
                if ((control != null && ReferenceEquals(e.Control, control))
                    || (instruments != null && ReferenceEquals(e.Instruments, instruments)))
                    _registered.RemoveAt(i);
            }

            for (int i = 0; i < _registered.Count; i++)
            {
                if (!ReferenceEquals(_registered[i].Hull, hull)) continue;
                _registered[i] = new Entry(control, instruments, hull);
                Regrant();
                return;
            }

            _registered.Add(new Entry(control, instruments, hull));
            Regrant();
        }

        /// <summary>A relay standing down (its component disabled or destroyed), naming the hull it
        /// registered. Idempotent — a hull that was never registered, or already dropped, is a no-op.
        ///
        /// <para>⚠ Pass the token you REGISTERED with, held since, rather than re-resolving it: a relay
        /// whose controller has been destroyed under it would re-resolve to something else and leave its
        /// entry standing.</para></summary>
        public void Unregister(object hull)
        {
            if (hull == null) return;
            for (int i = 0; i < _registered.Count; i++)
            {
                if (!ReferenceEquals(_registered[i].Hull, hull)) continue;
                _registered.RemoveAt(i);
                break;
            }
            Regrant();
        }

        /// <summary>
        /// <b>The player is piloting THIS hull</b> — the declaration the whole arbitration turns on,
        /// published by the Player lane (<c>ControlSwitcher</c>) whenever its control mode or its boat
        /// changes. Pass null for "nobody is at a helm", which is the correct state on foot, on the deck,
        /// and while somebody else is steering.
        ///
        /// <para>Order-independent: declaring a hull no relay has registered yet simply grants nothing
        /// until one does, and a relay registering later is granted the moment it arrives.</para>
        /// </summary>
        public void SetPilotedHull(object hull)
        {
            _pilotedHull = hull;
            Regrant();
        }

        /// <summary>
        /// <b>The player is on THIS boat</b> — the wider declaration (<see cref="PlayersBoat"/>), for the
        /// readers that mean "my gear" rather than "my wheel". False when nothing is declared, when the
        /// boat asked about is null, and when either has been destroyed: no fallback here either, for the
        /// same reason the helm has none — "there is only one boat" is a guess, and it is wrong the day
        /// it stops being true.
        /// </summary>
        public bool IsPlayersBoat(object boat)
        {
            if (boat == null || _playersBoat == null) return false;
            if (!ReferenceEquals(_playersBoat, boat)) return false;
            return Alive(boat);
        }

        /// <summary>
        /// <b>The same question asked with a GAME OBJECT</b> — for a reader that holds the boat's
        /// ROOT rather than the controller token the Boats lane registered.
        ///
        /// <para><b>Why the overload exists.</b> The token this slot arbitrates on is deliberately an
        /// opaque <c>object</c> that Core never dereferences, and the Boats lane registers its
        /// <c>BoatController</c>. A lane that may not reference Boats — the Art lane, where the lamps
        /// live — has no way to name that instance: all it holds is the transform it is hanging off.
        /// Comparing a <c>Transform</c> to a <c>BoatController</c> by reference is simply false, and
        /// false in the quiet way, so the searchlight on the player's own boat would stop answering
        /// her switch with nothing anywhere to say why. Resolving both sides to the GameObject they
        /// live on is the one comparison that can be made honestly across that seam.</para>
        ///
        /// <para>Destroyed on either side is not the player's boat, for the same reason
        /// <see cref="Alive"/> exists at all: to Unity's <c>==</c> every destroyed object equals null
        /// and therefore equals every other destroyed object.</para>
        /// </summary>
        public bool IsPlayersBoat(UnityEngine.GameObject boatRoot)
        {
            if (boatRoot == null || _playersBoat == null) return false;
            if (!Alive(_playersBoat)) return false;

            // ⚠️ NOT `as GameObject ?? (as Component)?.gameObject`. `??` tests for a REAL null and a
            // destroyed UnityEngine.Object is not one — it is a live reference that merely compares
            // equal to null — so the coalesce would hand back the destroyed object instead of falling
            // through. Alive() above already refuses that case; spelling the branch out keeps the two
            // facts independent rather than relying on the guard to cover a hole in the expression.
            UnityEngine.GameObject mine = _playersBoat as UnityEngine.GameObject;
            if (mine == null)
            {
                var component = _playersBoat as UnityEngine.Component;
                if (component == null) return false;
                mine = component.gameObject;
            }
            if (mine == null) return false;
            return ReferenceEquals(mine, boatRoot);
        }

        /// <summary>
        /// <b>The player is aboard THIS boat</b> — at the helm or on her deck. Published by the Player
        /// lane alongside <see cref="SetPilotedHull"/>; null when she is ashore or driving.
        /// </summary>
        public void SetPlayersBoat(object boat) => _playersBoat = boat;

        /// <summary>True when this relay is the granted one — the question "am I the player's helm?",
        /// asked by the relay itself (<c>IsPlayerHelm</c>) and by the dev instrument keys.</summary>
        public bool Holds(IHelmControl control)
        {
            if (control == null) return false;
            if (!GrantStillGood()) Regrant();
            return ReferenceEquals(_control, control);
        }

        /// <summary>Drop every registration and the declaration with it (scene teardown / tests).</summary>
        public void Reset()
        {
            _registered.Clear();
            _pilotedHull = null;
            _playersBoat = null;
            _control = null;
            _instruments = null;
        }

        // ---- arbitration ------------------------------------------------------------------------

        /// <summary>
        /// Re-decide who holds the slot: the registration whose hull IS the declared piloted hull, or
        /// nobody. Dead registrations are dropped on the way past — a relay whose GameObject was
        /// destroyed without its OnDisable running (a teardown storm, DestroyImmediate in a test) must
        /// not be handed out, and a hull that no longer exists is not a hull anybody is piloting.
        /// </summary>
        private void Regrant()
        {
            // A registration outlives its usefulness when the HULL is gone, or when neither of its two
            // faces is left to hand out. A single dead face is not a dead entry — it is nulled at the
            // grant below, so half a relay cannot be published as if it were whole.
            for (int i = _registered.Count - 1; i >= 0; i--)
            {
                Entry e = _registered[i];
                if (!Alive(e.Hull) || (!Alive(e.Control) && !Alive(e.Instruments)))
                    _registered.RemoveAt(i);
            }

            _control = null;
            _instruments = null;
            if (_pilotedHull == null || !Alive(_pilotedHull)) return;

            for (int i = 0; i < _registered.Count; i++)
            {
                Entry e = _registered[i];
                if (!ReferenceEquals(e.Hull, _pilotedHull)) continue;
                if (Alive(e.Control)) _control = e.Control;
                if (Alive(e.Instruments)) _instruments = e.Instruments;
                return;
            }
        }

        /// <summary>
        /// Is the standing answer still good? A grant of NOBODY always is — the answer can only change
        /// through <see cref="Register"/>/<see cref="Unregister"/>/<see cref="SetPilotedHull"/>, and a
        /// death can invalidate a grant but never create one. A live grant is good while both its relay
        /// and the declared hull still exist. So the common ashore read costs one null compare, and the
        /// piloting read costs two native liveness checks — no list walk, no allocation, per frame
        /// (rule 7). A false answer sends the read through <see cref="Regrant"/> exactly once.
        /// </summary>
        private bool GrantStillGood()
        {
            if (_control == null && _instruments == null) return true;   // nothing granted, nothing to go stale
            return Alive(_pilotedHull)
                   && (_control == null || Alive(_control))
                   && (_instruments == null || Alive(_instruments));
        }

        /// <summary>
        /// Is this token still a real thing? Anything that is not a <c>UnityEngine.Object</c> is taken at
        /// face value (a plain token cannot be destroyed); a Unity object is asked through its OWN
        /// overloaded <c>==</c>, the only operator that reports a destroyed native object as null.
        ///
        /// <para>⚠ Never write this as <c>o ?? fallback</c>, <c>o?.x</c> or <c>o is null</c>: those
        /// compare the raw C# reference, which a destroyed object still has, so a fake-null sails
        /// straight through them.</para>
        /// </summary>
        private static bool Alive(object token)
        {
            if (token is UnityEngine.Object unityObject) return unityObject != null;
            return token != null;
        }
    }
}
