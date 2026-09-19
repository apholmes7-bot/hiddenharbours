using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The player, drawn as one skinned mesh through the iso facet pass</b> (ADR 0044 option d):
    /// ABOARD behind <see cref="GameConfig.MeshCharacter"/>, and ASHORE only while
    /// <see cref="GameConfig.MeshCharacterAshore"/> is on as well.
    ///
    /// <para><b>What this component is.</b> A reader. It computes nothing about the player: stance,
    /// facing, gait, the deck point under her boots and her bearing on that deck are all decided by
    /// <see cref="DeckRiderVisual"/> and consumed here at the same seam the sprite consumes them at.
    /// Its whole job is (state, gait) → clip key → frame → one posed mesh.</para>
    ///
    /// <para><b>Two figures, and at most one of them a frame.</b> ABOARD the figure hangs under the
    /// hull's posed mesh child at the deck point the occupant slot is already fed from, so only a
    /// facet hull can carry her: aboard a SPRITE hull she keeps her sprite. ASHORE a second figure
    /// stands under her own body at her feet and draws through the facet pass under a figure id of
    /// her own (<see cref="IsoCharacterFigureRenderer.EnterAshore"/>, #861). The rider's child sprite
    /// draws her aboard and her body sprite draws her ashore; each figure stands in for exactly one of
    /// them, on the same call that hides it, so on every frame ONE of the four draws her, or none
    /// where she is inside something. It never touches <c>IsoFacetHullFeature</c> or
    /// <c>IsoFacetHullRegistry</c>: the ashore gate is the figure renderer's, and the id budget is
    /// its own charter's.</para>
    ///
    /// <para><b>⚠ <c>aboard == false</c> is NOT "ashore".</b> The rider also says false in a cab and at
    /// a helm that hides its pilot. So the ashore figure draws only while the rider says her body is
    /// shown on the root (<see cref="DeckRiderVisual.BodyShownOnRoot"/>), no clip is playing and she is
    /// dry (<see cref="WhyNotShownAshore"/>). Any clip (the boarding vault, the ladders, the haul, the
    /// chop, the bench, sleep, swim, the open machines' Drive) and wading hand the draw back to the
    /// sprite for their length: the mesh follows stance and gait only and has no pose for any of
    /// them. The held item stays at the sprite's hand.</para>
    ///
    /// <para><b>⚠ Her body is hidden with <see cref="Renderer.forceRenderingOff"/>, never
    /// <c>enabled</c>.</b> <c>enabled</c> is the rider's stand-down's alone, and the sprite shadow and
    /// the see-through-foliage outline key on it, so both keep her sprite's shape while the mesh draws.
    /// It is set only after <c>EnterAshore</c> said yes, on the call that shows the mesh, and given
    /// back on the call that hides it.</para>
    ///
    /// <para><b>The id is asked for at ARRIVALS, never per frame</b> (<see cref="AshoreArrivals"/>).
    /// A refusal (a crowded region: Nine Mile Creek on a cold start) keeps her whole sprite, is
    /// latched, and is asked again only at her next arrival ashore. A granted id is kept for as long as
    /// the switch stays on, aboard and through every clip; it goes back when a switch goes off or this
    /// component is disabled, destroyed or re-configured.</para>
    ///
    /// <para><b>Occlusion aboard is per pixel and costs nothing.</b> The figure wears the hull's own
    /// <c>HHHullFacet</c> pass and shares her private depth buffer with <c>ZWrite On / ZTest
    /// LEqual</c>, exactly as a fitting does (<see cref="IsoFacetPropRenderer"/>). A gunwale in front
    /// of her boots is in front of her boots because it is nearer, not because anybody discarded
    /// anything. The 12-slot <c>DeckOccupants</c> publication is untouched and still fed by
    /// <see cref="DeckRiderVisual"/> alone — this component adds no second occlusion path, it reads the
    /// stand point that publication is already built from
    /// (<see cref="DeckRiderVisual.DeckStandRigLocal"/>).</para>
    ///
    /// <para><b>⚠ What she will NOT look like yet.</b> These debts are stated, not paid:</para>
    /// <list type="bullet">
    ///   <item><b>The look.</b> The facet shader is the boat's, and against the inked art the figure
    ///   measures 43–57% off. That is the shader look pass, not this one.</item>
    ///   <item><b>The face.</b> The sprite's face is a raster stamp the mesh does not carry: she has
    ///   no eyes, brows or mouth. Nothing here invents them.</item>
    ///   <item><b>The counter-lean and the head look.</b> Rig 7 never exported the additive bone
    ///   table its brief promised (§2.5) — its only rock surface bakes the lean into frames and the
    ///   extractor passes no options, so every baked clip is the zero-rock pose. Deriving those
    ///   rotations in C# would be authoring look in code. The whole-body rock stays the live hull
    ///   transform it already is; the additive layer is owed upstream to <c>art-director</c>.</item>
    ///   <item><b>Ashore, her shadow and her outline are the sprite's</b> (the owner's ruling of
    ///   2026-09-19, decision 6): a mesh shadow ashore is not this lane.</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DeckRiderVisual))]
    public sealed class DeckRiderMeshPresenter : MonoBehaviour, ICharacterFigure
    {
        [Tooltip("The baked CharacterSkinDef to draw. Left empty, the presenter asks the character's " +
                 "own CharacterVisualDef for its Skin — one art def, one answer, so the mesh and the " +
                 "sheets can never describe two different people.")]
        [SerializeField] private CharacterSkinDef _skin;

        private const string AshoreSwitchOffReason = "ashore — GameConfig.MeshCharacterAshore is off";

        private const string AshoreRefusedReason =
            "ashore — EnterAshore refused her (the facet-id pool is used up): she keeps her whole sprite " +
            "until her next arrival ashore";

        // ---------------------------------------------------------------- sabotage (tests only)

        /// <summary>
        /// <b>Sabotage: freeze the mesh on frame 0.</b> Arms the determinism/stepping guard — with it
        /// set, "a later game time draws a later pose" must FAIL.
        ///
        /// <para>⚠ It feeds the MESH and nothing else. A knob that also held the sprite would leave
        /// both sides agreeing and the guard would pass while measuring nothing.</para>
        /// </summary>
        [System.NonSerialized] public bool SabotageHoldFrameZero;

        /// <summary>
        /// <b>Sabotage: shift the figure off the published stand point</b>, in rig metres. Arms the
        /// placement guard — with it set, "the figure stands where the occupant slot says she stands"
        /// must FAIL. Mesh-only for the same reason.
        /// </summary>
        [System.NonSerialized] public Vector3 SabotageStandOffsetMetres;

        // ---------------------------------------------------------------- state

        private DeckRiderVisual _rider;
        private IsoCharacterFigureRenderer _figure;
        private Transform _figureRoot;
        private IsoFacetHullRenderer _hull;
        private CharacterSkinDef _configured;
        private string _stateKey;
        private double _stateStartSeconds;

        // Ashore. A figure of her own under her body, the body it is hiding (and only while it is),
        // and the latch that decides when the id pool may be asked again.
        private IsoCharacterFigureRenderer _ashoreFigure;
        private CharacterSkinDef _ashoreConfigured;
        private string _ashoreStateKey;
        private double _ashoreStateStartSeconds;
        private SpriteRenderer _hiddenBody;
        private readonly AshoreArrivals _arrivals = new AshoreArrivals();
        private CharacterClipPlayer _clipPlayer;
        private PlayerWalkController _walk;

        // ---------------------------------------------------------------- published

        /// <inheritdoc/>
        /// <remarks>True on a frame either figure draws. The rider reads it only aboard, where it holds
        /// the rider's child sprite off; ashore the body is held off by this component itself.</remarks>
        public bool DrawsInsteadOfSprite { get; private set; }

        /// <summary>The ABOARD figure, once one exists. Null before she has ever drawn aboard.</summary>
        public IsoCharacterFigureRenderer Figure => _figure;

        /// <summary>The facet hull she is riding, or null.</summary>
        public IsoFacetHullRenderer Hull => _hull;

        /// <summary>The def actually in use aboard — the serialized one, or the character art def's.</summary>
        public CharacterSkinDef Skin => _configured;

        /// <summary>The ABOARD figure's clip key drawn this frame, or null. Ashore, read
        /// <see cref="AshoreFigure"/>.</summary>
        public string DrawnStateKey => _figure != null ? _figure.DrawnStateKey : null;

        /// <summary>The aboard clip frame drawn this frame (after the poisoned-frame fence), or -1.</summary>
        public int DrawnFrame => _figure != null ? _figure.DrawnFrame : -1;

        /// <summary>The aboard frame ASKED for before the fence, or -1 — the pair a test compares.</summary>
        public int RequestedFrame => _figure != null ? _figure.RequestedFrame : -1;

        /// <summary>True when the clip the state map ASKED the def for was not in it and a shorter one
        /// stood in — i.e. the BAKE came up short.
        ///
        /// <para>⚠ This does NOT flag the helm/oars divergence. Helm and Oars map to the GAIT key
        /// before the def is ever consulted (there is no <c>helm</c> clip to miss), so the def carried
        /// exactly what was asked for and nothing fell back — while the sprite path, whose FisherIso def
        /// DOES carry helm and oars sheets, draws a different pose entirely. That divergence is a
        /// stated debt of this PR, not something this flag reports.</para></summary>
        public bool FellBackToGait { get; private set; }

        /// <summary>Where the aboard figure was placed, in the hull's posed-mesh local metres.</summary>
        public Vector3 FigureLocalMetres { get; private set; }

        /// <summary>The yaw applied to the aboard figure about the rig's up axis, in degrees.</summary>
        public float FigureYawDegrees { get; private set; }

        /// <summary>Her ASHORE figure, once one exists: built the first time she is shown ashore with
        /// both switches on, and kept, with its id, until a switch goes off or this component is
        /// disabled, destroyed or re-configured. Null with the ashore switch off.</summary>
        public IsoCharacterFigureRenderer AshoreFigure => _ashoreFigure;

        /// <summary>True on a frame the ASHORE figure draws and her body sprite is held off by
        /// <see cref="Renderer.forceRenderingOff"/>. A statement about THIS frame, written false at the
        /// top of every pose, never a latch.</summary>
        public bool DrawsAshore { get; private set; }

        /// <summary>True from the pose the facet-id pool refused her until her next arrival ashore.</summary>
        public bool AshoreRefused => _arrivals.Refused;

        /// <summary>The yaw given to the ashore figure, in degrees about the rig's up: her sprite's
        /// compass heading through the skin's measured azimuth sign.</summary>
        public float AshoreYawDegrees { get; private set; }

        /// <summary>
        /// <b>Why she is not drawing as a mesh</b>, in words, or null while she is. Published because
        /// every one of the reasons below is a silent, correct-looking no-op — the sprite simply keeps
        /// drawing — and a plate that came back as the sprite would otherwise not say which gate shut.
        /// </summary>
        public string NotDrawingReason { get; private set; } = "not posed yet";

        /// <summary>Point the presenter at a skin explicitly. The test path, and the override for a
        /// character whose art def does not name one.</summary>
        public void Configure(CharacterSkinDef skin)
        {
            _skin = skin;
            Release();
            ReleaseAshore();
        }

        // ---------------------------------------------------------------- lifecycle

        private void Awake()
        {
            _rider = GetComponent<DeckRiderVisual>();
        }

        private void OnEnable()
        {
            if (_rider == null) _rider = GetComponent<DeckRiderVisual>();
            // Register with the rider rather than have the rider find us: the rider owns the sprite
            // and must keep working with no figure at all, so the dependency only ever points this way.
            if (_rider != null) _rider.SetFigureOverride(this);
        }

        private void OnDisable()
        {
            if (_rider != null) _rider.SetFigureOverride(null);
            DrawsInsteadOfSprite = false;
            NotDrawingReason = "presenter disabled";
            Release();
            ReleaseAshore();   // her body is hers again: nobody is left to give it back later
        }

        private void OnDestroy()
        {
            Release();
            ReleaseAshore();
        }

        // ---------------------------------------------------------------- the one path

        /// <inheritdoc/>
        /// <remarks>Called ONCE per frame from <see cref="DeckRiderVisual"/>'s own <c>LateUpdate</c>
        /// (and synchronously at each of its re-seats), BEFORE it decides whether to enable the sprite —
        /// never from this component's own <c>Update</c>, because the answer depends on values
        /// <c>DeckRiderVisual</c> publishes and a second opinion about execution order is exactly the
        /// kind of second authority this lane was told not to invent. The stand IS the rider
        /// (<see cref="ICharacterFigureStand"/>, moved into Core by ADR 0044's 2026-09-17 amendment so
        /// the cast can take the same seam).</remarks>
        public void PoseFigure(ICharacterFigureStand stand, bool aboard)
        {
            DrawsInsteadOfSprite = false;
            DrawsAshore = false;

            if (stand == null || (stand is Object standObject && standObject == null))
            {
                HideAshore();
                Stop("no rider");
                return;
            }
            if (!aboard) { PoseAshore(stand); return; }

            // ⭐ ABOARD, her ashore figure hides and her body is given back BEFORE the rider mirrors
            // that body onto its own child and disables it: one call, one frame, and the id stays
            // hers. With the ashore switch off there is no ashore figure, and one left over from a
            // switch turned off at run time gives her id back here.
            GameConfig config = GameServices.Config;
            if (AshoreSwitchOn(config)) HideAshore(); else ReleaseAshore();

            if (config == null) { Stop("no GameConfig"); return; }
            if (!config.MeshCharacter) { Stop("GameConfig.MeshCharacter is off"); return; }

            CharacterSkinDef skin = ResolveSkin(stand);
            if (skin == null) { Stop("no CharacterSkinDef — none set here and none on the art def"); return; }
            if (!skin.IsUsable()) { Stop($"CharacterSkinDef '{skin.Id}' is not usable — re-bake it"); return; }

            IsoFacetHullRenderer hull = ResolveHull(stand);
            if (hull == null) { Stop("the hull under her is not a facet mesh hull"); return; }

            IsoCharacterSprite character = stand.FigureCharacter;
            if (character == null) { Stop("no IsoCharacterSprite to read stance and gait from"); return; }

            // ⚠ Stance is read as REQUESTED, not as DRAWN. DrawnStance is whatever survived the SHEET
            // ladder, so it reports on the SPRITE's art coverage rather than on what she is doing: any
            // def whose stance sheet is short collapses it to Free, and the mesh would then never reach
            // its own baked `balance` clip however well the bake covered it. Reading the request keeps
            // both paths on ONE authority, one step earlier — DeckRiderVisual wrote it itself, this
            // frame, in StateContext.
            if (!CharacterSkinStateMap.Resolve(skin, character.Stance, character.Gait,
                                               out string stateKey, out bool fellBack))
            {
                Stop($"no clip for stance {character.Stance} / gait {character.Gait}");
                return;
            }
            FellBackToGait = fellBack;

            // ADR 0041: the switch is PER STATE. A state not listed still plays its sheet, which is
            // how the sheets retire one at a time at parity instead of all at once on a promise.
            if (!skin.DrawsAsMesh(stateKey))
            {
                Stop($"state '{stateKey}' is not in CharacterSkinDef.MeshStates (ADR 0041)");
                return;
            }

            if (!EnsureFigure(skin, hull)) { Stop("the figure could not be built"); return; }
            if (!skin.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip)) { Stop($"clip '{stateKey}' vanished"); return; }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            if (!string.Equals(stateKey, _stateKey, System.StringComparison.Ordinal))
            {
                _stateKey = stateKey;
                _stateStartSeconds = now;
            }

            // ⚠ RULE 5. A LOOPING clip runs from absolute game time, so the pose is a pure function of
            // (worldSeed, gameTime) and replays identically on a reload and on another machine; the
            // seed-derived phase still keeps two figures out of lockstep. A ONE-SHOT clip is a
            // TRANSITION and has a beginning — it runs from the moment its state was entered, and
            // holds its last frame rather than wrapping.
            double clipStart = clip.Loop ? 0d : _stateStartSeconds;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            int frame = SabotageHoldFrameZero
                        ? 0
                        : CharacterSkinPose.FrameFor(clip, seed, now, clipStart);

            if (!_figure.SetPose(stateKey, frame)) { Stop($"could not pose '{stateKey}' frame {frame}"); return; }

            Place(stand, skin);
            _figure.Visible = true;
            DrawsInsteadOfSprite = true;
            NotDrawingReason = null;
        }

        /// <summary>
        /// <b>Ashore</b> (the rider said <c>aboard == false</c>). With a switch off this is main's
        /// frame exactly: the aboard figure stands down and nothing else is touched. With both on, her
        /// ashore figure draws instead of her body sprite while <see cref="WhyNotShownAshore"/> says
        /// she is shown on her own feet, her state is a mesh state and the id pool has not refused her.
        /// </summary>
        private void PoseAshore(ICharacterFigureStand stand)
        {
            GameConfig config = GameServices.Config;
            if (!AshoreSwitchOn(config))
            {
                // ⭐ SWITCH OFF IS MAIN'S FRAME, byte for byte: no ashore figure, no figure id, and her
                // body exactly as the rider leaves it. A figure left over from a switch turned off at
                // run time gives her id back and goes; the aboard figure stands down as it always did.
                ReleaseAshore();
                Stop(config == null ? "no GameConfig"
                     : !config.MeshCharacter ? "GameConfig.MeshCharacter is off"
                     : AshoreSwitchOffReason);
                return;
            }

            // The aboard figure stands down exactly as it always has ashore (hidden, kept).
            StandDownAboard();

            SpriteRenderer body = _rider != null ? _rider.BodyRenderer : null;
            string notShown = WhyNotShownAshore(
                riderLive: _rider != null && _rider.isActiveAndEnabled,
                hasRiderChild: _rider != null && _rider.HasRider,
                hasBody: body != null,
                bodyShownOnRoot: _rider != null && _rider.BodyShownOnRoot,
                clipPlaying: ClipPlaying(),
                water: WaterAtFeet());
            if (notShown != null) { StopAshore(notShown); return; }

            // Shown ashore on her own feet. An ARRIVAL clears a refusal, so the pool is asked once more.
            _arrivals.Step(true, _rider.ReseatCount);

            // Somebody else hid her body (a cutscene, a future cutaway): not ours to show or to give back.
            if (body.forceRenderingOff && _hiddenBody != body)
            {
                HoldSprite("ashore — her body's forceRenderingOff was set by something other than this figure");
                return;
            }

            CharacterSkinDef skin = ResolveSkin(stand);
            if (skin == null) { HoldSprite("no CharacterSkinDef — none set here and none on the art def"); return; }
            if (!skin.IsUsable()) { HoldSprite($"CharacterSkinDef '{skin.Id}' is not usable — re-bake it"); return; }

            IsoCharacterSprite character = stand.FigureCharacter;
            if (character == null) { HoldSprite("no IsoCharacterSprite to read stance and gait from"); return; }

            // Stance as REQUESTED, exactly as aboard (see there).
            if (!CharacterSkinStateMap.Resolve(skin, character.Stance, character.Gait,
                                               out string stateKey, out bool fellBack))
            {
                HoldSprite($"no clip for stance {character.Stance} / gait {character.Gait}");
                return;
            }
            FellBackToGait = fellBack;

            if (!skin.DrawsAsMesh(stateKey))
            {
                HoldSprite($"state '{stateKey}' is not in CharacterSkinDef.MeshStates (ADR 0041)");
                return;
            }

            // Refused at this arrival or an earlier one: the whole sprite, and no second ask until the
            // next arrival (the registry has already said so once, for this ask).
            if (_arrivals.Refused) { HoldSprite(AshoreRefusedReason); return; }

            if (!EnsureAshoreFigure(skin, body)) { HoldSprite("the ashore figure could not be built"); return; }
            if (!skin.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip)) { HoldSprite($"clip '{stateKey}' vanished"); return; }

            // ⭐ THE ID, AND THE SAME-FRAME SORT. Already ashore, this only re-points her overlay at her
            // body and writes this frame's sort and position: YSortSprite wrote the body's order at
            // execution order 0 and this is the rider's LateUpdate at 100, so the copy is this frame's,
            // never last frame's. Not yet ashore, it asks the pool for her id.
            bool granted;
            try
            {
                granted = _ashoreFigure.EnterAshore(body);
            }
            catch (System.Exception e)
            {
                // A throw would skip the rest of the rider's frame (its stand-down writes the rider child
                // and the body AFTER this call), so it is caught, logged for this ask, and held as a
                // refusal: she keeps her sprite until her next arrival.
                Debug.LogException(e, this);
                granted = false;
            }
            if (!granted)
            {
                _arrivals.Refuse();
                HoldSprite(AshoreRefusedReason);
                return;
            }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            if (!string.Equals(stateKey, _ashoreStateKey, System.StringComparison.Ordinal))
            {
                _ashoreStateKey = stateKey;
                _ashoreStateStartSeconds = now;
            }
            // RULE 5, exactly as aboard: loops from absolute game time, one-shots from their entry.
            double clipStart = clip.Loop ? 0d : _ashoreStateStartSeconds;
            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            int frame = SabotageHoldFrameZero
                        ? 0
                        : CharacterSkinPose.FrameFor(clip, seed, now, clipStart);
            if (!_ashoreFigure.SetPose(stateKey, frame)) { HoldSprite($"could not pose '{stateKey}' frame {frame}"); return; }

            // Her facing: the sprite's own compass heading, through the def's MEASURED azimuth sign. The
            // ashore frame is a hull frame at heading 0, where a deck bearing and a compass heading are
            // the same number, so this is the aboard rule with the hull taken out.
            float heading = character.HeadingDegrees;
            float yaw = skin.AzimuthCounterClockwise ? -heading : heading;
            _ashoreFigure.SetAshoreYaw(yaw);
            AshoreYawDegrees = yaw;

            // ONE FIGURE, NOT TWO: the mesh shows and the body hides on the same call.
            _ashoreFigure.Visible = true;
            body.forceRenderingOff = true;
            _hiddenBody = body;
            DrawsInsteadOfSprite = true;
            DrawsAshore = true;
            NotDrawingReason = null;
        }

        /// <summary>Both switches: <see cref="GameConfig.MeshCharacterAshore"/> is live only with
        /// <see cref="GameConfig.MeshCharacter"/> on too (the owner's ruling of 2026-09-19, decision 1).</summary>
        public static bool AshoreSwitchOn(GameConfig config) =>
            config != null && config.MeshCharacter && config.MeshCharacterAshore;

        /// <summary>
        /// <b>The ashore hand-over rule</b>, as a pure function of what the rider, the clip player and
        /// the walk controller publish: null when she is SHOWN ASHORE on her own feet and her mesh may
        /// stand in for her body sprite, else the reason she is not (the owner's ruling of 2026-09-19,
        /// decision 4, and the NEITHER rule). Static, so EditMode holds it to the rule without a frame.
        /// </summary>
        public static string WhyNotShownAshore(bool riderLive, bool hasRiderChild, bool hasBody,
                                               bool bodyShownOnRoot, bool clipPlaying,
                                               OnFootWaterState water)
        {
            if (!riderLive) return "ashore — the rider is disabled, so nothing poses her each frame";
            if (!hasRiderChild) return "ashore — no rider child is wired, so the rider cannot tell a deck from the shore";
            if (!hasBody) return "ashore — there is no body sprite to stand in for";
            if (!bodyShownOnRoot) return "inside something (a cab, or a helm that hides its pilot): she draws neither";
            if (clipPlaying) return "ashore — a clip is playing, and the sprite draws every clip";
            if (water == OnFootWaterState.Wade) return "ashore — wading, and the sprite draws her in the water";
            if (water != OnFootWaterState.Dry) return "ashore — swimming, and the sprite draws her in the water";
            return null;
        }

        /// <summary>
        /// <b>When her ashore figure may ask the facet-id pool again</b> (the owner's ruling of
        /// 2026-09-19, decision 3, option (i)). A pure state machine, so EditMode holds it to the rule.
        ///
        /// <para>An ARRIVAL is the first pose she is shown ashore after one she was not (a landing, a
        /// clip's end, a wade's end, the switch turned on), or a shown pose on which the rider has
        /// re-seated her since (<see cref="DeckRiderVisual.ReseatCount"/>: a region's arrival re-states
        /// OnFoot while she is already standing ashore). A refusal is latched and only an arrival clears
        /// it, so a pool at its end is asked once per arrival and never per frame.</para>
        /// </summary>
        public sealed class AshoreArrivals
        {
            private bool _wasShown;
            private int _seenReseat;

            /// <summary>True from a refusal until the next arrival.</summary>
            public bool Refused { get; private set; }

            /// <summary>Step one pose. True when it is an ARRIVAL, which clears a refusal.</summary>
            public bool Step(bool shownAshore, int reseatCount)
            {
                bool arrival = shownAshore && (!_wasShown || reseatCount != _seenReseat);
                _wasShown = shownAshore;
                _seenReseat = reseatCount;
                if (arrival) Refused = false;
                return arrival;
            }

            /// <summary>The pool said no: hold the sprite until the next arrival.</summary>
            public void Refuse() => Refused = true;

            /// <summary>Forget everything: the next shown pose is an arrival.</summary>
            public void Reset()
            {
                _wasShown = false;
                _seenReseat = 0;
                Refused = false;
            }
        }

        /// <summary>
        /// Stand the ABOARD figure down and say why. Hides rather than destroys: boarding and
        /// dismounting are frequent enough on a working deck that rebuilding a posed mesh, a material and
        /// two ramp textures each time would be a stutter the player can see (rule 7). The build is only
        /// thrown away when the hull under her actually changes.
        /// </summary>
        private void Stop(string reason)
        {
            NotDrawingReason = reason;
            StandDownAboard();
        }

        private void StandDownAboard()
        {
            if (_hull == null) { Release(); return; }   // Unity-null: the hull was destroyed under her
            if (_figure != null) _figure.Visible = false;
        }

        /// <summary>Not shown ashore this pose (aboard, inside, in a clip, in the water): the ashore
        /// figure hides and KEEPS her id, her body is hers again, and the arrival latch hears she left.</summary>
        private void HideAshore()
        {
            _arrivals.Step(false, _rider != null ? _rider.ReseatCount : 0);
            if (_ashoreFigure != null) _ashoreFigure.Visible = false;
            GiveBodyBack();
        }

        private void StopAshore(string reason)
        {
            HideAshore();
            NotDrawingReason = reason;
        }

        /// <summary>Shown ashore, but the mesh does not draw this pose: the same hand-back, without
        /// telling the arrival latch she left.</summary>
        private void HoldSprite(string reason)
        {
            NotDrawingReason = reason;
            if (_ashoreFigure != null) _ashoreFigure.Visible = false;
            GiveBodyBack();
        }

        /// <summary>The body this component hid, and only that one, drawn again.</summary>
        private void GiveBodyBack()
        {
            if (_hiddenBody != null) _hiddenBody.forceRenderingOff = false;
            _hiddenBody = null;
        }

        /// <summary>A missing clip player plays nothing.</summary>
        private bool ClipPlaying()
        {
            if (_clipPlayer == null) TryGetComponent(out _clipPlayer);
            return _clipPlayer != null && _clipPlayer.IsPlaying;
        }

        /// <summary>A missing walk controller is dry ground.</summary>
        private OnFootWaterState WaterAtFeet()
        {
            if (_walk == null) TryGetComponent(out _walk);
            return _walk != null ? _walk.WaterState : OnFootWaterState.Dry;
        }

        // ---------------------------------------------------------------- resolution

        /// <summary>
        /// The skin: the one set here, else the one named by the character's own
        /// <see cref="CharacterVisualDef"/>. Going through the art def rather than a second serialized
        /// reference means the sheets and the mesh cannot describe two different people, and it is the
        /// same place every other fact about how this character looks already lives (ADR 0003).
        /// </summary>
        private CharacterSkinDef ResolveSkin(ICharacterFigureStand stand)
        {
            if (_skin != null) return _skin;
            IsoCharacterSprite character = stand.FigureCharacter;
            CharacterVisualDef visual = character != null ? character.Visual : null;
            return visual != null ? visual.Skin : null;
        }

        /// <summary>
        /// The facet hull she is standing on — through the rider's OWN hull resolution (the stand's
        /// <see cref="ICharacterFigureStand.FigureHull"/>, which <see cref="DeckRiderVisual"/> answers
        /// from <see cref="DeckRiderVisual.LiveHullPresenter"/>), never a second search of the scene. The
        /// presenter seam answers with the visual transform the skinner installed the renderer on, so
        /// a boat re-skinned under her feet answers with the new one on the very next frame.
        ///
        /// <para>A sprite hull answers null here, and that is correct rather than unfortunate: a
        /// sprite hull records no facet block, so there is no pass to draw a mesh figure through.</para>
        /// </summary>
        private IsoFacetHullRenderer ResolveHull(ICharacterFigureStand stand)
        {
            Transform visual = stand.FigureHull;
            if (visual == null) return null;
            IsoFacetHullRenderer hull = visual.GetComponent<IsoFacetHullRenderer>();
            return hull != null && hull.PosedMesh != null ? hull : null;
        }

        // ---------------------------------------------------------------- the figures

        private bool EnsureFigure(CharacterSkinDef skin, IsoFacetHullRenderer hull)
        {
            if (_figure != null && _hull == hull && _configured == skin) return true;

            Release();
            _hull = hull;
            _configured = skin;

            Transform posed = hull.PosedMesh;
            var go = new GameObject("MeshCharacter") { hideFlags = HideFlags.DontSave };
            go.layer = posed.gameObject.layer;      // the hull's layer, or the facet pass never sees her
            go.transform.SetParent(posed, false);
            _figureRoot = go.transform;

            // Parent FIRST, configure second: IsoCharacterFigureRenderer finds its hull by walking up
            // the parent chain, and a figure configured while unparented would carry no hull id.
            _figure = go.AddComponent<IsoCharacterFigureRenderer>();
            _figure.Configure(skin);
            _figure.Visible = false;
            _stateKey = null;
            return _figure.IsConfigured;
        }

        /// <summary>
        /// Her ashore figure, built once: under her body at its pivot (her feet), on her body's layer,
        /// because its overlay quad is what sorts against sprites on the camera that draws her body.
        /// Never under a hull, so <see cref="IsoCharacterFigureRenderer.EnterAshore"/>'s parent refusal
        /// cannot trip: aboard, her root hangs off the boat's physics root and the hull renderer sits on
        /// a visual child of that root, a sibling of hers.
        /// </summary>
        private bool EnsureAshoreFigure(CharacterSkinDef skin, SpriteRenderer body)
        {
            if (_ashoreFigure != null && _ashoreConfigured == skin) return _ashoreFigure.IsConfigured;

            ReleaseAshoreFigure();
            var go = new GameObject("MeshCharacterAshore") { hideFlags = HideFlags.DontSave };
            go.layer = body.gameObject.layer;
            go.transform.SetParent(body.transform, false);
            _ashoreFigure = go.AddComponent<IsoCharacterFigureRenderer>();
            _ashoreFigure.Configure(skin);
            _ashoreFigure.Visible = false;
            _ashoreConfigured = skin;
            _ashoreStateKey = null;
            return _ashoreFigure.IsConfigured;
        }

        /// <summary>
        /// <b>Where she stands, and which way she faces — both borrowed, neither computed.</b>
        ///
        /// <para>The position is <see cref="DeckRiderVisual.DeckStandRigLocal"/>: the very point the
        /// deck-occupant slot is fed with, published at the line that feeds it. The hull's posed mesh
        /// child's local space IS the rig frame in metres — <c>IsoFacetHullRenderer.ViewDepthOf</c>
        /// proves it, since it answers a slot's depth by transforming exactly this vector through
        /// exactly this transform. So the figure's foot and her occluder band are, by construction,
        /// the same point.</para>
        ///
        /// <para>The yaw is the deck bearing, about the rig's up axis (+z), through the def's own
        /// MEASURED azimuth sign. This is not a guessed convention: the hull's heading enters the same
        /// frame as <c>Rz(dirUnits·45°)</c> with the identical negation
        /// (<c>HullMeshMath.HeadingToDirUnits</c>), and the bind mesh is baked at rig dir 0, so a
        /// figure at deck bearing 0 faces the bow. This lane has been CCW-mislabelled twice; the sign
        /// is read off the bake, not declared here.</para>
        /// </summary>
        private void Place(ICharacterFigureStand stand, CharacterSkinDef skin)
        {
            Vector3 local = stand.FigureStandRigMetres + SabotageStandOffsetMetres;
            float bearing = stand.FigureDeckBearingDegrees;
            float yaw = skin.AzimuthCounterClockwise ? -bearing : bearing;
            FigureLocalMetres = local;
            FigureYawDegrees = yaw;
            _figureRoot.SetLocalPositionAndRotation(local, Quaternion.AngleAxis(yaw, Vector3.forward));
        }

        /// <summary>The ABOARD figure, thrown away. The ashore one is <see cref="ReleaseAshore"/>'s.</summary>
        private void Release()
        {
            if (_figureRoot != null) DestroySafely(_figureRoot.gameObject);
            _figureRoot = null;
            _figure = null;
            _hull = null;
            _configured = null;
            _stateKey = null;
            FellBackToGait = false;
        }

        /// <summary>Everything ashore undone: her body given back, her id returned, her figure gone,
        /// and the arrival latch forgotten, so the next shown pose is an arrival.</summary>
        private void ReleaseAshore()
        {
            GiveBodyBack();
            ReleaseAshoreFigure();
            _arrivals.Reset();
            DrawsAshore = false;
        }

        private void ReleaseAshoreFigure()
        {
            if (_ashoreFigure != null)
            {
                // The id goes back NOW, not at the end of the frame when the deferred destroy lands.
                _ashoreFigure.LeaveAshore();
                DestroySafely(_ashoreFigure.gameObject);
            }
            _ashoreFigure = null;
            _ashoreConfigured = null;
            _ashoreStateKey = null;
        }

        private static void DestroySafely(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
