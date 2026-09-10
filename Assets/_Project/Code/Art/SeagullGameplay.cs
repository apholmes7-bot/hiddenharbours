using System;
using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The runtime shape of <c>docs/art/rigs/gameplay/seagullIsoRig.gameplay.json</c> — schema
    /// <c>hidden-harbours/creature-gameplay@1</c>, the herring gull's behaviour contract.
    ///
    /// <para>Data only: no UnityEngine types, no behaviour, no defaults invented here. It is filled
    /// by <c>HiddenHarbours.Tools.RigBaking.SeagullSidecarReader</c>, which is editor-side because
    /// the pin it enforces hashes <c>docs/art/rigs/seagullIsoRig.js</c> — and <c>docs/</c> is not in
    /// a build. Keeping the DATA here, and engine-light, is what lets the bird's state machine
    /// consume it without a rewrite, and what lets a baker serialise it into <c>Assets/</c> later
    /// the way <see cref="CatchStorageAnchors"/> is serialised today.</para>
    ///
    /// <para><b>The sidecar is generated, never hand-edited</b> — its own <c>authoring</c> field says
    /// so. A number that looks wrong is fixed upstream in the rig and re-exported; nothing in this
    /// file may "correct" one on the way past.</para>
    ///
    /// <para><b>Absence is data.</b> The schema's <c>extractor_contract</c> reads: "per section: rig
    /// export -&gt; this sidecar -&gt; absent section = the creature does not support the feature
    /// (not an error)". So every optional section carries a <c>Has…</c> flag: a missing WATER is not
    /// the same fact as a WATER whose numbers are zero, and a reader that cannot tell them apart
    /// hands the caller a silent zero.</para>
    /// </summary>
    [Serializable]
    public sealed class SeagullGameplay
    {
        // ---- provenance -------------------------------------------------------------------------

        /// <summary>The schema string as the file declares it.</summary>
        public string Schema = "";

        /// <summary>The rig this was cut from, by name, e.g. "seagullIsoRig.js".</summary>
        public string RigFileName = "";

        /// <summary>The global the rig publishes, e.g. "SeagullIso".</summary>
        public string ExportSymbol = "";

        /// <summary>The rig digest the sidecar was stamped with. The pin itself lives in the reader;
        /// this is carried so a consumer can report it without re-hashing.</summary>
        public string DerivedFromRigSha256 = "";

        // ---- frame ------------------------------------------------------------------------------

        /// <summary>Cell width in px (64). From the rig, never from a README (ADR 0021 §4).</summary>
        public int CellWidth;

        /// <summary>Cell height in px (64).</summary>
        public int CellHeight;

        /// <summary>Pivot x in cell px (32).</summary>
        public int PivotX;

        /// <summary>
        /// Pivot y in cell px from the TOP of the cell (46) — the contact point: ground when
        /// standing, walking or perched, the water surface when floating, and the point straight
        /// below the bird when airborne.
        ///
        /// <para><b>Altitude is a screen OFFSET, never a sprite scale.</b> An airborne frame blits at
        /// <c>pivot − altitude·cos(40°)·32·zoom</c> px and the shadow stays at the pivot, so the bird
        /// descends onto its own shadow. The gull is 1.40 m across at EVERY altitude: 45 px, always.
        /// There are no giant gulls crossing the camera.</para>
        /// </summary>
        public int PivotY;

        /// <summary>32 px = 1 m — the world scale the whole game is drawn at.</summary>
        public float PixelsPerMetre;

        /// <summary>Facings baked (8).</summary>
        public int Directions;

        /// <summary>What dir 0 depicts, verbatim from the file.</summary>
        public string Dir0 = "";

        /// <summary>The frame's origin, verbatim.</summary>
        public string FrameOrigin = "";

        /// <summary>The frame's axes, verbatim.</summary>
        public string FrameAxes = "";

        // ---- creature ---------------------------------------------------------------------------

        public string Label = "";
        public float LengthMetres;

        /// <summary>1.40 m — 45 px on the sheet, at every altitude.</summary>
        public float WingspanMetres;

        public float MassKg;
        public float DraftMetres;

        /// <summary>Body-centre height above the contact plane while standing.</summary>
        public float BodyCentreZStand;

        /// <summary>Body-centre height above the perch top.</summary>
        public float BodyCentreZPerch;

        /// <summary>Body-centre height above the water surface while floating.</summary>
        public float BodyCentreZFloat;

        /// <summary>Standing footprint (x, y) in metres.</summary>
        public float FootprintStandX, FootprintStandY;

        /// <summary>Footprint with the wings open (x, y) in metres — what a landing needs clear.</summary>
        public float FootprintWingsOpenX, FootprintWingsOpenY;

        // ---- STATES / TRANSITIONS ---------------------------------------------------------------

        /// <summary>The thirteen animation states, in the file's own order.</summary>
        public readonly List<SeagullState> States = new List<SeagullState>();

        /// <summary>The declared state graph. See <see cref="SeagullTransition"/> for how many edges
        /// there are and how many the kit README claims.</summary>
        public readonly List<SeagullTransition> Transitions = new List<SeagullTransition>();

        /// <summary>The state with this id, or null.</summary>
        public SeagullState State(string id)
        {
            for (int i = 0; i < States.Count; i++)
                if (string.Equals(States[i].Id, id, StringComparison.Ordinal)) return States[i];
            return null;
        }

        /// <summary>Is this edge declared? Anything not declared is not a legal move.</summary>
        public bool HasTransition(string from, string to)
        {
            for (int i = 0; i < Transitions.Count; i++)
                if (string.Equals(Transitions[i].From, from, StringComparison.Ordinal) &&
                    string.Equals(Transitions[i].To, to, StringComparison.Ordinal)) return true;
            return false;
        }

        // ---- LAND -------------------------------------------------------------------------------

        /// <summary>False when the section is absent — the creature does not land, which is a fact,
        /// not a zero.</summary>
        public bool HasLand;

        /// <summary>The clear box a landing needs, (x, y) in metres — 1.40 × 0.60, i.e. the open
        /// wingspan.</summary>
        public float LandNeedsClearX, LandNeedsClearY;

        public bool LandApproachIntoWind;

        /// <summary>Minimum flat run under the pivot for the landing to end in <c>stand</c> rather
        /// than <c>perch</c>.</summary>
        public float LandMinFlatMetres;

        public string LandNote = "";

        // ---- PERCH ------------------------------------------------------------------------------

        public bool HasPerch;
        public float PerchMinWidthMetres;
        public float PerchMaxWidthForGripMetres;
        public float PerchFlatOkMinMetres;
        public float PerchMaxSlopeDegrees;
        public float PerchClearanceAboveMetres;

        /// <summary>What the art director named as perchable — the vocabulary a prop or hull sidecar's
        /// <c>ANCHORS</c> of type "perch" is expected to draw from. A prop that declares none is not
        /// perchable; that is the contract, not an omission.</summary>
        public readonly List<string> PerchCandidates = new List<string>();

        /// <summary>Left foot offset at dir 0, gripping the perch edge.</summary>
        public SeagullOffsetPx PerchFootLeft;

        /// <summary>Right foot offset at dir 0.</summary>
        public SeagullOffsetPx PerchFootRight;

        /// <summary>The only state a perch exits to.</summary>
        public string PerchExit = "";

        public string PerchNote = "";

        // ---- WATER ------------------------------------------------------------------------------

        public bool HasWater;
        public float FloatBodyZMetres;

        /// <summary>Where the bill meets the surface at dir 0 — the point <c>peck</c> dips to.</summary>
        public SeagullOffsetPx BillAtSurfacePx;

        /// <summary>The frame of <c>splash</c> the burst fires on (2). A fish reaction hangs off this
        /// FRAME, not off the state's start.</summary>
        public int SplashBurstFrame;

        public string SplashRig = "";
        public bool DriftWithCurrent;

        /// <summary>A dive is a feeding strike: the page rolls a catch at <c>splash</c> f2.</summary>
        public bool HasDiveYield;
        public float DiveCatchProbability;
        public readonly List<string> DiveCatchSpecies = new List<string>();
        public string DiveCatchRig = "";

        public string WaterNote = "";

        // ---- FLOCK ------------------------------------------------------------------------------

        public bool HasFlock;

        /// <summary>What a flock forms on. <b>Wire only the attractors that exist in the game
        /// today</b> — the rest are the art director naming a vocabulary, not a promise that
        /// something is emitting one.</summary>
        public readonly List<SeagullAttractor> Attractors = new List<SeagullAttractor>();

        public int FlockSizeMin, FlockSizeMax;
        public float FlockRadiusMetres;
        public float FlockAltitudeMinMetres, FlockAltitudeMaxMetres;
        public float FlockPeriodMinSeconds, FlockPeriodMaxSeconds;
        public float FlockGlideDuty;
        public float FlockSwoopEveryMinSeconds, FlockSwoopEveryMaxSeconds;
        public float FlockSpacingMetres;

        /// <summary>A flock is readable from 40 m — that is the gameplay point of it.</summary>
        public float FlockAttractRadiusMetres;

        /// <summary>Inside this, a bird takes off.</summary>
        public float FlockFleeRadiusMetres;

        public float FlockSettleAfterSeconds;
        public string FlockFleeReaction = "";
        public float FlockRegroupSeconds;

        /// <summary>
        /// The tub-stealing contract, read so that it is not lost — <b>not a licence to build it.</b>
        /// Stealing from tubs, fouling trap stacks and the deterring dog are DECLARED here and were
        /// not part of what the owner asked for; they are logged, not built.
        /// </summary>
        public bool HasSteal;
        public string StealFrom = "";
        public float StealPerBirdSeconds;
        public string StealTakes = "";
        public readonly List<string> StealDeterredBy = new List<string>();

        public string FlockNote = "";

        // ---- ANCHORS_PX -------------------------------------------------------------------------

        /// <summary>
        /// Screen offsets from the pivot at dir 0, keyed by contact state ("stand", "float",
        /// "perch") then by part ("bill", "head", "wingL", "wingR", "tail", "footL", "footR" —
        /// <c>float</c> carries no feet, because they are under the water).
        ///
        /// <para>dir 0 only. Every other facing comes from <c>SeagullIso.anchors(dir,{anim,frame})</c>
        /// at bake time; do not rotate these by hand.</para>
        /// </summary>
        public readonly Dictionary<string, Dictionary<string, SeagullOffsetPx>> AnchorsPx =
            new Dictionary<string, Dictionary<string, SeagullOffsetPx>>(StringComparer.Ordinal);

        /// <summary>False when the section is absent.</summary>
        public bool HasAnchors => AnchorsPx.Count > 0;

        /// <summary>One anchor, or null when that state or part is not declared.</summary>
        public SeagullOffsetPx Anchor(string contactState, string part)
        {
            return AnchorsPx.TryGetValue(contactState, out var parts) &&
                   parts.TryGetValue(part, out var p) ? p : null;
        }
    }

    /// <summary>A screen-space offset from the cell pivot, with the height it was measured at.</summary>
    [Serializable]
    public sealed class SeagullOffsetPx
    {
        /// <summary>Cell px right of the pivot.</summary>
        public float Dx;

        /// <summary>Cell px <b>DOWN</b> the screen from the pivot — the rigs' convention throughout.
        /// Unity's y is up: negate on the way in, once, at the seam.</summary>
        public float Dy;

        /// <summary>Bird-local height in metres above the contact plane.</summary>
        public float Z;
    }

    /// <summary>One animation state, exactly as the sidecar states it.</summary>
    [Serializable]
    public sealed class SeagullState
    {
        public string Id = "";

        /// <summary>Frame count — also the number of sheet columns this state occupies.</summary>
        public int Frames;

        /// <summary>Milliseconds PER FRAME, not for the whole state.</summary>
        public int Milliseconds;

        /// <summary>Ground speed the page should move the bird at, m/s.</summary>
        public float SpeedMetresPerSecond;

        /// <summary>Vertical rate, m/s. Negative descends.</summary>
        public float ClimbMetresPerSecond;

        /// <summary>
        /// The first number of the state's <c>alt</c> pair, metres.
        ///
        /// <para><b>⚠️ The pair does not mean the same thing in every state.</b> For the looping
        /// states it is a [min, max] BAND the bird may sit anywhere in (fly 6–25, glide 3–25, swoop
        /// 1–8). For the one-shots it is START → END, and the order carries the sign of the move:
        /// land [0.6, 0] and takeoff [0, 1.2] both read start-to-end. <c>dive</c> does NOT — it ships
        /// [0.3, 8] while the kit README writes the same state "8→0.3", so the file states dive's
        /// band ascending and the prose states its travel descending. A consumer that assumes
        /// [start, end] uniformly will fly the dive UPWARDS. Use
        /// <see cref="AltitudeMinMetres"/>/<see cref="AltitudeMaxMetres"/> for the band and read
        /// <see cref="ClimbMetresPerSecond"/> — unambiguous, and −9.0 for a dive — for direction.</para>
        /// </summary>
        public float AltitudeA;

        /// <summary>The second number of the <c>alt</c> pair. See <see cref="AltitudeA"/>: the pair is
        /// not uniformly start→end.</summary>
        public float AltitudeB;

        public float AltitudeMinMetres => Math.Min(AltitudeA, AltitudeB);
        public float AltitudeMaxMetres => Math.Max(AltitudeA, AltitudeB);

        public bool Loop;

        /// <summary>The state a one-shot falls into, or "" when it loops.</summary>
        public string Next = "";

        /// <summary>Whether the state declares a <c>travel</c> distance.</summary>
        public bool HasTravel;

        /// <summary>Metres the one-shot carries the bird forward over its whole run.</summary>
        public float TravelMetres;

        /// <summary>Whether the state declares a <c>cycles</c> range — how many times a loop should
        /// repeat before the page moves on.</summary>
        public bool HasCycles;

        public int CyclesMin, CyclesMax;

        /// <summary>The art director's note, verbatim, or "".</summary>
        public string Note = "";

        /// <summary>How long one pass of the state takes, ms.</summary>
        public int DurationMilliseconds => Frames * Milliseconds;
    }

    /// <summary>
    /// One declared edge of the gull's state graph.
    ///
    /// <para><b>⚠️ There are 26 of them.</b> The kit README says "TRANSITIONS — 27 edges" and the
    /// intake charter repeated it; the rig's own <c>TRANSITIONS</c> literal and the generated
    /// sidecar both hold 26, all distinct, none naming an unknown state. The rig is the authority —
    /// the same lesson the shovel's azimuth header taught — so 26 is what the tests pin, and the
    /// discrepancy went back to the seat rather than being papered over here.</para>
    /// </summary>
    [Serializable]
    public sealed class SeagullTransition
    {
        public string From = "";
        public string To = "";
        public override string ToString() => From + "->" + To;
    }

    /// <summary>Something a flock forms on.</summary>
    [Serializable]
    public sealed class SeagullAttractor
    {
        public string Id = "";

        /// <summary>Relative pull, 0..1.</summary>
        public float Weight;

        /// <summary>Where the art director expects it to come from, verbatim. <b>Several of these
        /// have no emitter in the game today</b>; the list is a vocabulary, not a wiring plan.</summary>
        public string Source = "";
    }
}
