using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>DEV-ONLY: step the piloted hull's BROW instrument, in place, at the helm.</b> Press <c>K</c> and
    /// the flush cutout walks <b>bare → depth sounder → fish finder → bare</b> on the boat under you, so
    /// every instrument ADR 0025 has shipped can actually be LOOKED AT. <see cref="DevBoatPicker"/>'s
    /// <c>F</c> walks the fleet; this walks the glass. Together they are the whole dev-visibility grid: four
    /// consoled hulls × three brow states, with two keys and no shop.
    ///
    /// <para><b>Why a cycle and not a bigger grant.</b> The obvious "dev mode owns everything" would HIDE
    /// instruments rather than reveal them: <see cref="BoatEquipment.EffectiveFit"/> resolves ONE brow
    /// cutout, and the fish finder deliberately WINS it over the depth sounder. Grant both ids and the
    /// plain sounder becomes permanently unreachable — the opposite of the ask. A cycle is the only shape
    /// that can show each unit on its own, which is why it must also be able to express FEWER instruments
    /// than the hull ships (see <see cref="DevFit"/>).</para>
    ///
    /// <para><b>Nothing here is a purchase.</b> The step is applied by widening the relay's per-read
    /// SCRATCH owned-id list — the exact mechanism <see cref="HelmControlRelay.DevIgnoreEquipmentGating"/>
    /// has used since S2 — and by narrowing the RESOLVED fit. Neither touches
    /// <see cref="InstrumentLocker"/>, so no dev convenience can ever reach the player's save and
    /// permanently fit an instrument they never bought (rule 5: the fit is recomputed, never stored).</para>
    ///
    /// <para><b>Inert in a shipped build.</b> There is ONE dev predicate in this system and the relay owns
    /// it: <see cref="HelmControlRelay.DevIgnoreEquipmentGating"/>, which is false outside the editor and a
    /// development build. This component reads that flag rather than re-deriving it, so the two can never
    /// drift apart and a player can never key their way to a free fish finder. The component is mounted
    /// unconditionally (the <see cref="MastheadTelltale"/>/<see cref="HelmControlRelay"/> precedent —
    /// self-installed from <see cref="BoatController"/>'s Awake so every already-built scene grows it with
    /// no builder re-run); it is the BEHAVIOUR that is gated, not the mount.</para>
    ///
    /// <para><b>The tier carries across an F-swap</b> — see <see cref="HelmControlRelay.DevBrowStep"/> for
    /// why, and for what happens on a console that cannot take the carried unit.</para>
    ///
    /// <para>New Input System only (<c>Keyboard.current</c>) — legacy <c>UnityEngine.Input</c> compiles and
    /// then THROWS at runtime in this project. The key is a serialized field, so the owner can rebind it
    /// without touching code.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevInstrumentCycle : MonoBehaviour
    {
        [Header("Keys (owner-editable)")]
        [Tooltip("Step the brow instrument: bare → depth sounder → fish finder → bare. K for Kit — free " +
                 "of every other binding in the project (F fleet/interact, V variant, Z neutral, X dump, " +
                 "N tide, T rotation/trap, Y auto-yaw, G grant, H haul, L spotlight/lid, O displaced " +
                 "water, I ice box, J/U character spike, P buy, B sell, E interact, Q mooring, Space " +
                 "work/pull, WASD+arrows helm, Esc close).")]
        [SerializeField] private Key _cycleKey = Key.K;

        private BoatController _boat;
        private HelmControlRelay _relay;

        // Reused read buffer — the school query fills a caller-owned list (rule 7). One press, one reuse.
        private readonly List<FishSchool> _schoolScratch = new List<FishSchool>(8);

        /// <summary>The key that steps the brow (diagnostics / the key ledger's other end).</summary>
        public Key CycleKey => _cycleKey;

        /// <summary>Wire explicitly (tests / rigs built without Awake order guarantees).</summary>
        public void Configure(BoatController boat, HelmControlRelay relay)
        {
            _boat = boat;
            _relay = relay;
        }

        private void Awake()
        {
            if (_boat == null) _boat = GetComponent<BoatController>();
            if (_relay == null) _relay = GetComponent<HelmControlRelay>();
        }

        private void Update()
        {
            if (_relay == null) _relay = GetComponent<HelmControlRelay>();
            // T2: ONE dev predicate, owned by the relay. False in a shipped build ⇒ the key is dead.
            if (_relay == null || !_relay.DevIgnoreEquipmentGating) return;
            // Only at the helm, and only on the boat the player is actually piloting — the picker's own
            // gate. A second boat in the scene (a rotation rig, a test harness) never eats the keypress.
            if (_boat == null || !_boat.isActiveAndEnabled) return;
            if (!ReferenceEquals(GameServices.HelmInstruments, _relay)) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[_cycleKey].wasPressedThisFrame) Step();
        }

        /// <summary>
        /// Advance the brow one step and report what landed. Public so tests can drive the cycle without
        /// the input loop. A hull with no console says so rather than silently eating the press — the
        /// <see cref="DevBoatPicker.ToggleHullVariant"/> posture.
        /// </summary>
        public void Step()
        {
            if (_relay == null) _relay = GetComponent<HelmControlRelay>();
            if (_relay == null) return;

            HelmConsoleDef console = Console();
            if (console == null)
            {
                EventBus.Publish(new DevNotice("This hull has no helm console — no brow to cycle."));
                return;
            }

            SounderKind next = NextBrow(_relay.DevBrowStep, console.SupportsFishFinder);
            _relay.DevBrowStep = next;

            SounderKind shown = Reachable(next, console);
            EventBus.Publish(new DevNotice($"Brow → {Label(shown)}"));
            Debug.Log($"[DevInstrumentCycle] {_relay.HullId} brow → {shown} " +
                      $"(console {console.Id}, fish-finder slot {(console.SupportsFishFinder ? "yes" : "no")}). " +
                      $"Press {_cycleKey} again for the next.");

            if (shown == SounderKind.Fish) ReportSchools();
        }

        // ---- the sonar's legibility (a pure READ of the Core fish seam) --------------------------------

        /// <summary>
        /// Say out loud what the finder is about to draw. Schools are deterministic and sparse, so the
        /// owner will often cycle onto the finder over empty water — and an honest empty sonar looks
        /// exactly like a broken one. This names the sea instead: how many schools are under the
        /// transducer, and where the nearest one's centre lies.
        ///
        /// <para><b>Reads, never writes.</b> <see cref="IFishSchools.SchoolsAt"/> at the SAME position the
        /// finder queries <see cref="IFishSchools.MarksAt"/> from (<see cref="IHelmInstruments.TryReadPosition"/>),
        /// so the log and the glass can never disagree. Nothing here spawns, moves or forces a school —
        /// they are recomputed from <c>(worldSeed, gameTime)</c> and this is a spectator.</para>
        ///
        /// <para>⚠ <b>"In range" means ON, not NEAR.</b> The seam's contract is containment — a school owns
        /// its own radius and the query asks which areas contain this point (<see cref="FishSchool"/>) —
        /// so there is no "there is one 200 m north" to report and this deliberately does not invent one.
        /// "Nearest" is the nearest CENTRE among the schools already under the boat, which is the useful
        /// number: it says which way to nudge for a stronger return.</para>
        /// </summary>
        public string ReportSchools()
        {
            string line = SchoolLine();
            Debug.Log(line);
            return line;
        }

        private string SchoolLine()
        {
            IGameClock clock = GameServices.Clock;
            if (clock == null)
                return "[DevInstrumentCycle] sonar: no clock — the school sim has no time to be asked about.";
            if (_relay == null || !_relay.TryReadPosition(out Vector2 pos))
                return "[DevInstrumentCycle] sonar: no transducer position — nothing to query.";

            int n = GameServices.FishSchools.SchoolsAt(pos, clock.TotalSeconds, _schoolScratch);
            if (n <= 0)
                return $"[DevInstrumentCycle] sonar at ({pos.x:F1}, {pos.y:F1}): NO school under the " +
                       "transducer. The seam reports schools you are ON, not ones nearby — an empty " +
                       "sonar here is the truth, not a broken instrument.";

            int nearest = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _schoolScratch.Count; i++)
            {
                float d = (_schoolScratch[i].Centre - pos).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                nearest = i;
            }

            FishSchool s = _schoolScratch[nearest];
            Vector2 toCentre = s.Centre - pos;
            // The ONE bearing convention in the project (0 = N, +90 = E) — never a second atan2.
            float bearing = BoatKinematics.BearingDegrees(toCentre);
            return $"[DevInstrumentCycle] sonar at ({pos.x:F1}, {pos.y:F1}): {n} school(s) under the " +
                   $"transducer. Nearest centre bearing {bearing:F0}° at {toCentre.magnitude:F1} m; " +
                   $"fish at {s.DepthMetres:F1} m; {s.MarkCount} mark(s); signal {s.SignalAt(pos):F2}.";
        }

        // ---- the pure rules (EditMode-testable without a scene) ----------------------------------------

        /// <summary>
        /// The cycle ring: <b>None → Depth → Fish → None</b>, with the finder step SKIPPED on a console
        /// whose brow cannot take it. Skipping rather than showing-and-hoping matters because
        /// <see cref="BoatEquipment.EffectiveFit"/> would silently swallow the unsupported id and leave the
        /// brow where it was — the key would look like it had been dropped. All four shipped helms take the
        /// finder today, so this is about not lying the first time one does not.
        /// </summary>
        public static SounderKind NextBrow(SounderKind current, bool supportsFishFinder)
        {
            switch (current)
            {
                // The basic sounder needs no support flag: a helm console is BY DEFINITION a dash with a
                // brow, which is exactly why BoatEquipment has no SupportsSounder knob.
                case SounderKind.None:  return SounderKind.Depth;
                case SounderKind.Depth: return supportsFishFinder ? SounderKind.Fish : SounderKind.None;
                default:                return SounderKind.None;
            }
        }

        /// <summary>
        /// The brow this console can ACTUALLY show for a requested step. The cycle ring never asks for an
        /// unsupported unit, but the requested step CARRIES across an F-swap, so it can land on a hull that
        /// refuses it: fall back to the nearest unit below rather than drawing an instrument the hull could
        /// never carry. The stored step is left alone, so F back onto a capable hull restores the finder.
        /// </summary>
        public static SounderKind Reachable(SounderKind requested, HelmConsoleDef console)
        {
            if (console == null) return SounderKind.None;                  // no dash, nothing to mount in
            if (requested == SounderKind.Fish && !console.SupportsFishFinder) return SounderKind.Depth;
            return requested;
        }

        /// <summary>
        /// The dev cycle's effective fit for one console — the whole override, in one pure function.
        ///
        /// <para><b>WIDEN, then NARROW.</b> Widening is the mechanism the dev bypass has always used: the
        /// step's instrument id joins the caller's SCRATCH owned list, so
        /// <see cref="BoatEquipment.EffectiveFit"/>'s own slot gating still has the final say and this
        /// function owns no second copy of it. Narrowing is the half a grant structurally cannot do: the
        /// Novi and Cape consoles ship a depth sounder in their AUTHORED default, and a real purchase can
        /// never be un-bought, so "show me a bare brow" is unreachable by owning less. The resolved fit's
        /// brow is therefore clamped to the step — the only place the cycle can express FEWER instruments
        /// than the hull carries, and the reason the owner can still see an empty cutout at all.</para>
        ///
        /// <para>Everything else in the fit (rig, compass, radar, gps) reads through untouched: this is a
        /// BROW cycle, not a dev mode that re-fits the whole dash.</para>
        /// </summary>
        public static HelmFit DevFit(HelmConsoleDef console, List<string> ownedScratch, SounderKind step)
        {
            if (console == null) return HelmFit.None;

            SounderKind brow = Reachable(step, console);
            string id = brow == SounderKind.Fish ? BoatEquipment.FishFinderId
                      : brow == SounderKind.Depth ? BoatEquipment.DepthSounderId
                      : null;
            if (id != null && ownedScratch != null && !ownedScratch.Contains(id)) ownedScratch.Add(id);

            HelmFit fit = BoatEquipment.EffectiveFit(console, ownedScratch);
            if (fit.Sounder == brow) return fit;
            return new HelmFit(fit.Rig, brow, fit.Compass, fit.Radar, fit.Gps);
        }

        // ---- helpers ------------------------------------------------------------------------------------

        private HelmConsoleDef Console()
        {
            if (_boat == null) _boat = GetComponent<BoatController>();
            // ⚠ No `?.`/`??` on a UnityEngine.Object — they bypass the overloaded `==` and read a destroyed
            // asset as live ([[unity-null-coalescing-defeats-fake-null]]).
            if (_boat == null || _boat.Hull == null) return null;
            HelmConsoleDef console = _boat.Hull.Helm;
            return console != null ? console : null;
        }

        private static string Label(SounderKind brow) => brow switch
        {
            SounderKind.Depth => "depth sounder",
            SounderKind.Fish  => "fish finder",
            _                 => "bare (no sounder)",
        };
    }
}
