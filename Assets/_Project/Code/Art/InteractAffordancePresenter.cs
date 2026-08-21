using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The thing you can touch looks touchable</b> — the render-side half of the interaction seam.
    ///
    /// <para><b>The problem, in the owner's words (2026-08-19).</b> At interior closeups an interactable
    /// vanishes into the room: a stairwell, a bed and the freezer are invisible-until-pressed. There is
    /// now a popup naming what you face, but the words are in the lower right and the thing is in the
    /// middle of a dim room, and the player has to guess which object the sentence is about. This lights
    /// the object.</para>
    ///
    /// <para><b>It rides <see cref="InteractOfferChanged"/>, not
    /// <see cref="InteractCandidateChanged"/></b> — a correction to this lane's own brief, and the reason
    /// matters. The brief was written before the popup landed and named the older candidate channel. But
    /// the brief's binding constraint is that the affordance and the popup must never disagree, and the
    /// popup rides the OFFER: the offer is the arbitrated answer across all three readers of the interact
    /// key, the candidate signal is the fixture registry's answer alone. Riding the candidate channel
    /// would light a bed while the popup said "Talk — Aunt Ginny", which is precisely the disagreement the
    /// constraint forbids. Core's own remarks say the same thing from the other side: the offer carries a
    /// position and an id expressly for "the affordance lane".</para>
    ///
    /// <para><b>Nothing here recomputes candidacy</b> (one quantity, one computation). It subscribes,
    /// matches, and draws. It also does not re-derive whether the offer may be SHOWN: that rule lives in
    /// <see cref="InteractOfferVisibility"/> and the popup reads the same one, which is what makes "they
    /// never disagree" structural rather than a thing two files promise each other.</para>
    ///
    /// <para><b>How it draws, and why it is an overlay.</b> Two pooled child renderers — one BEHIND the
    /// fixture (the outline) and one OVER it (the lift) — copy the fixture's sprite and pose each frame
    /// while armed. The fixture's own renderer is never touched: not its material, not its colour, not
    /// its sorting. That is the only way to satisfy "never dirty the shared asset" on props that draw on
    /// Unity's DEFAULT sprite material (<c>InteriorCatalog.ConfigureProp</c> assigns none), and it means
    /// an un-highlighted room is byte-for-byte the room that shipped.</para>
    ///
    /// <para><b>Rule 7.</b> Two renderers exist for the whole session and are toggled, never created;
    /// nothing allocates after <see cref="Build"/>; while nothing is offered the per-frame cost is one
    /// struct compare and an early return, which is the same shape (and the same justification) as
    /// <c>InteractPopup.Update</c>.</para>
    ///
    /// <para><b>SCAFFOLD.</b> Three variants ship behind the dev menu so the owner can pick one on a walk.
    /// The two that lose are to be DELETED, not left as dead toggles — see the PR.</para>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class InteractAffordancePresenter : MonoBehaviour
    {
        private static InteractAffordancePresenter _instance;

        /// <summary>
        /// Self-install once when the game starts, following <c>InteractPopup</c>'s precedent exactly —
        /// and for its reason: the fixtures this lights are in already-saved scenes, so an affordance that
        /// needed a builder re-run would be missing from every region opened for review until somebody
        /// remembered to re-bake it.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("InteractAffordance");
            _instance = go.AddComponent<InteractAffordancePresenter>();
        }

        /// <summary>The installed presenter, or null. For a test that wants the live one.</summary>
        public static InteractAffordancePresenter Instance => _instance;

        /// <summary>
        /// Which look is worn. Static and session-local: the dev menu cycles it, nothing serializes it,
        /// and when the owner picks a winner this whole switch goes away with the losers.
        /// </summary>
        public static InteractAffordanceVariant Variant { get; set; } = InteractAffordanceVariant.Both;

        // ---- tunables (rule 6: no magic numbers) ------------------------------------------------

        [Tooltip("Persist across scene loads like the popup and the services. The affordance is always-on.")]
        [SerializeField] private bool _persistAcrossScenes = true;

        [Tooltip("How thick the outline reads, in sprite pixels, at the sprite's widest.")]
        [SerializeField] private float _outlinePixels = 1f;

        [Tooltip("How much of its own colour a lifted pixel gains. One ramp step on this art.")]
        [SerializeField] private float _liftAmount = 0.35f;

        [Tooltip("How far down its own ramp the outline sits. Near zero is the ramp floor.")]
        [SerializeField] private float _outlineDarken = 0.18f;

        private const string ShaderName = "HiddenHarbours/InteractAffordance";

        // Sorting offsets either side of the fixture. One step is enough because the fixture's own order
        // is re-derived every frame from YSortSprite and copied fresh (see SyncPose).
        private const int OutlineOrderOffset = -1;
        private const int LiftOrderOffset = 1;

        // ---- the two pooled overlays -------------------------------------------------------------

        private SpriteRenderer _outline;
        private SpriteRenderer _lift;
        private Material _outlineMaterial;
        private Material _liftMaterial;
        private MaterialPropertyBlock _mpb;

        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        // ---- state -------------------------------------------------------------------------------

        private InteractOfferChanged _offer;
        private InteractAffordanceState _state = InteractAffordanceState.Disarmed;
        private SpriteRenderer _target;
        private float _armedAt;
        private bool _drawn;

        // Which look is actually on the two renderers. Compared against the static every frame so the dev
        // menu takes effect WHERE THE OWNER IS STANDING — cycling three variants while facing one bed is
        // the whole point of the walk, and "step away and face it again" would make that a chore.
        private InteractAffordanceVariant _appliedVariant = InteractAffordanceVariant.Both;

        // A mesh-drawn or renderer-less candidate is a real gap, not a fault — but it should be said once
        // rather than every frame the player stands in front of it.
        private string _warnedFor;

        private void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
            if (_persistAcrossScenes && Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;

            // Runtime-created materials are ours alone and are not assets; nothing on disk is touched by
            // destroying them, and leaving them would leak one pair per session reload.
            if (_outlineMaterial != null) Destroy(_outlineMaterial);
            if (_liftMaterial != null) Destroy(_liftMaterial);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InteractOfferChanged>(OnOffer);

            // Catch up with whatever is already standing — the offer channel is edge-triggered, so an
            // affordance enabled after the player walked up to a fixture would otherwise sit dark.
            _offer = InteractOffer.Current;
            Apply();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractOfferChanged>(OnOffer);
            Hide();
        }

        private void OnOffer(InteractOfferChanged e) { _offer = e; Apply(); }

        /// <summary>
        /// Re-read the things that are FLAGS rather than signals, and keep the overlay glued to a fixture
        /// that may be moving or re-sorting under it.
        ///
        /// <para><b>Why LateUpdate and why execution order 100.</b> <c>YSortSprite</c> writes the fixture's
        /// <see cref="SpriteRenderer.sortingOrder"/> in its own LateUpdate; an overlay that copied the
        /// order earlier in the frame would be one frame stale and would flicker through the fixture as
        /// the player walked past it. Running after everything that can move or re-sort a fixture is the
        /// only ordering that is right by construction rather than by luck.</para>
        ///
        /// <para><b>And reconcile with <see cref="InteractOffer.Current"/></b>, which is the subscription's
        /// safety net rather than a second source — <c>InteractPopup</c>'s own reasoning, and it applies
        /// here for the same reason: this component is <c>DontDestroyOnLoad</c> and lives for the whole
        /// session, while one <see cref="EventBus.Clear{T}"/> in any suite would otherwise leave the
        /// shipped affordance permanently deaf (memory: EventBus.Clear UNSUBSCRIBES the component under
        /// test).</para>
        /// </summary>
        private void LateUpdate()
        {
            InteractOfferChanged live = InteractOffer.Current;
            if (!live.SameAs(_offer)) _offer = live;

            Apply();

            // Everything below is the cost of being ARMED. Nothing offered means one struct compare, the
            // Apply early-out, and this return.
            if (!_drawn) return;

            if (_appliedVariant != Variant) ApplyVariantVisibility();

            SyncPose();
            SyncStrength();
        }

        /// <summary>
        /// Decide what should be lit, and bind to it when that changes — the single place the drawn state
        /// is settled. Idempotent: called from the event handler and every frame.
        /// </summary>
        private void Apply()
        {
            InteractAffordanceState next = InteractAffordanceState.For(_offer);

            // Re-resolve while armed with nothing bound: the early-out holds only once a target is HELD.
            // That is deliberate rather than defensive — a fixture whose renderer arrives a frame late, or
            // one destroyed under a standing offer, both land here and settle on the next frame.
            if (next.SameAs(_state) && (!next.Armed || _target != null)) return;

            _state = next;

            if (!next.Armed) { Hide(); return; }

            // Build() failed to find the shader, so there is nothing to draw with. Arming anyway would
            // make IsShowing report a highlight nobody can see — and a test asserting on it would go
            // green over a blank screen.
            if (_lift == null && _outline == null) { Hide(); return; }

            _target = ResolveTarget(next.Id);
            if (_target == null) { Hide(); return; }

            // Phase is measured from the moment of arming, so the pulse always opens at full — the player
            // sees the edge at its strongest the instant they turn to face the thing.
            _armedAt = Time.time;
            _drawn = true;
            ApplyVariantVisibility();
            SyncPose();
            SyncStrength();
        }

        /// <summary>
        /// Which renderer wears the affordance for this offer id.
        ///
        /// <para><b>The lookup goes through Core</b> (<see cref="Interactables.TryGetTransform"/>), never
        /// through the registrant itself: the interaction contract's standing rule is that the art lane
        /// never references a registrant. Core answers WHERE; this decides WHAT TO DRAW, which is the only
        /// half that is art's business.</para>
        ///
        /// <para><b>An id with no registrant is normal, not a fault.</b> Transit offers are keyed by ACTION
        /// (<c>ControlStrings.IdBoard</c> and friends) and conversation offers by NPC id, and neither is in
        /// the fixture registry — so neither lights today. That is the honest state of this lane and is
        /// called out in the PR rather than papered over with a nearest-thing guess.</para>
        /// </summary>
        private SpriteRenderer ResolveTarget(string id)
        {
            if (!Interactables.TryGetTransform(id, out Transform where) || where == null) return null;

            var sprite = where.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null || sprite.sprite == null)
            {
                WarnOnce(id, where);
                return null;
            }

            return sprite;
        }

        /// <summary>
        /// Say ONCE that a candidate had nothing this lane can draw on. A fixture drawn on the mesh path
        /// lands here, which is the known gap — the mesh path does not model the sprite overlay at all,
        /// and it is flagged rather than guessed at (it also does not model <c>dith</c>, a separate open
        /// ruling this lane deliberately does not touch).
        /// </summary>
        private void WarnOnce(string id, Transform where)
        {
            if (string.Equals(_warnedFor, id, System.StringComparison.Ordinal)) return;
            _warnedFor = id;
            Debug.LogWarning(
                $"[InteractAffordance] '{id}' ({where.name}) has no SpriteRenderer, so there is nothing " +
                "for the affordance to draw on. Mesh-drawn candidates are a known gap in this lane — the " +
                "popup still names it; it just does not light.");
        }

        /// <summary>Stop drawing. Cheap and idempotent — the overlays keep existing, they just go dark.</summary>
        private void Hide()
        {
            _target = null;
            if (!_drawn) return;
            _drawn = false;
            if (_outline != null) _outline.enabled = false;
            if (_lift != null) _lift.enabled = false;
        }

        /// <summary>Which halves this variant wants on at all — re-read whenever the static moves, so a
        /// dev-menu cycle lands on the fixture the owner is already looking at.</summary>
        private void ApplyVariantVisibility()
        {
            _appliedVariant = Variant;
            if (_outline != null) _outline.enabled = InteractAffordanceStyle.UsesOutline(Variant);
            if (_lift != null) _lift.enabled = InteractAffordanceStyle.UsesLift(Variant);
        }

        /// <summary>
        /// Glue both overlays to the fixture: same sprite, same flip, same pose, adjacent sorting — and,
        /// for the outline, the same sprite drawn slightly larger about its own VISUAL CENTRE.
        ///
        /// <para><b>Why the centre and not the pivot.</b> These props pivot at the floor centre of their
        /// footprint, so scaling about the transform origin would grow the sprite upward and sideways and
        /// leave no edge along the bottom at all — the halo would sit off the object (memory:
        /// overlay-pose-rotates-about-origin-not-mount, the same trap wearing a different hat). Solving for
        /// the position that holds the sprite's centre still is exact arithmetic and costs one vector
        /// multiply.</para>
        /// </summary>
        private void SyncPose()
        {
            if (_target == null || _target.sprite == null) { Hide(); return; }

            Transform t = _target.transform;
            Sprite sprite = _target.sprite;

            CopyDraw(_lift, _target);
            CopyDraw(_outline, _target);

            // The lift sits exactly on the fixture: same pose, one sorting step over.
            if (_lift != null)
            {
                _lift.transform.SetPositionAndRotation(t.position, t.rotation);
                _lift.transform.localScale = t.lossyScale;
                _lift.sortingLayerID = _target.sortingLayerID;
                _lift.sortingOrder = _target.sortingOrder + LiftOrderOffset;
            }

            if (_outline != null)
            {
                float scale = OutlineScaleFor(sprite, _outlinePixels);

                // Hold the sprite's visual centre still while the copy grows around it. The centre of a
                // transform-posed sprite is p + r * (k * c); solve that for p at the larger scale.
                Vector3 c = sprite.bounds.center;
                Vector3 k = t.lossyScale;
                Vector3 grown = new Vector3(k.x * scale, k.y * scale, k.z);
                Vector3 centreWS = t.position + t.rotation * Vector3.Scale(k, c);

                _outline.transform.SetPositionAndRotation(
                    centreWS - t.rotation * Vector3.Scale(grown, c), t.rotation);
                _outline.transform.localScale = grown;
                _outline.sortingLayerID = _target.sortingLayerID;
                _outline.sortingOrder = _target.sortingOrder + OutlineOrderOffset;
            }
        }

        /// <summary>
        /// How much larger the outline copy is drawn, so its fringe reads as
        /// <paramref name="pixels"/> pixels at the sprite's widest.
        ///
        /// <para>Measured against the LARGER dimension deliberately: a uniform scale grows a long sprite
        /// more along its length than across it, so keying on the long axis means the requested thickness
        /// is a CEILING everywhere rather than an average that blows out on a wide bed. Pure arithmetic and
        /// public so a test can pin it.</para>
        /// </summary>
        public static float OutlineScaleFor(Sprite sprite, float pixels)
        {
            if (sprite == null || pixels <= 0f) return 1f;

            Vector2 size = sprite.bounds.size;
            float longest = Mathf.Max(size.x, size.y);
            if (longest <= Mathf.Epsilon) return 1f;

            float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 32f;
            float halo = pixels / ppu;                     // the fringe, in metres
            return (longest + 2f * halo) / longest;
        }

        /// <summary>Copy the things that decide what a sprite LOOKS like, as opposed to where it is.</summary>
        private static void CopyDraw(SpriteRenderer overlay, SpriteRenderer target)
        {
            if (overlay == null) return;
            overlay.sprite = target.sprite;
            overlay.flipX = target.flipX;
            overlay.flipY = target.flipY;
            overlay.sortingLayerID = target.sortingLayerID;
        }

        /// <summary>
        /// Push this frame's strength into both overlays. The pulse rides the OUTLINE only — the brief's
        /// breathing edge — while the lift is steady, because a lift that pulsed would read as the light
        /// in the room changing rather than as an affordance.
        /// </summary>
        private void SyncStrength()
        {
            float since = Time.time - _armedAt;
            float pulse = InteractAffordanceStyle.Pulse(since);

            _mpb ??= new MaterialPropertyBlock();

            if (_outline != null && _outline.enabled)
                SetStrength(_outline, InteractAffordanceStyle.OutlineStrength(Variant) * pulse);

            if (_lift != null && _lift.enabled)
                SetStrength(_lift, InteractAffordanceStyle.LiftStrength(Variant));
        }

        /// <summary>
        /// Write one renderer's strength through a <see cref="MaterialPropertyBlock"/>.
        ///
        /// <para><b>Never <c>sharedMaterial.SetFloat</c>.</b> That writes the MATERIAL, and when the
        /// material is an asset it dirties the asset on disk — a whole class of spurious diffs this project
        /// has been bitten by before. These two materials are runtime-created and are not assets, so the
        /// hazard does not strictly apply; the property block is used anyway because it is the habit that
        /// stays correct when somebody later swaps in a shipped material.</para>
        /// </summary>
        private void SetStrength(SpriteRenderer renderer, float strength)
        {
            renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(StrengthId, strength);
            renderer.SetPropertyBlock(_mpb);
        }

        // ---- the two overlays, built once --------------------------------------------------------

        private void Build()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[InteractAffordance] Shader '{ShaderName}' not found, so nothing can be highlighted. " +
                    "It is in Always Included Shaders, so this means the shader failed to compile — check " +
                    "the console for the ShaderLab error rather than assuming a missing file.");
                return;
            }

            // Additive: the fixture underneath keeps everything it already had and simply gains light.
            _liftMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _liftMaterial.SetFloat("_Mode", 0f);
            _liftMaterial.SetFloat("_LiftAmount", _liftAmount);
            _liftMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
            _liftMaterial.SetFloat("_DstBlend", (float)BlendMode.One);

            // Straight alpha: the outline is a dark edge, and adding a dark colour would add nothing.
            _outlineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _outlineMaterial.SetFloat("_Mode", 1f);
            _outlineMaterial.SetFloat("_OutlineDarken", _outlineDarken);
            _outlineMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _outlineMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

            _outline = MakeOverlay("Affordance_Outline", _outlineMaterial);
            _lift = MakeOverlay("Affordance_Lift", _liftMaterial);
        }

        private SpriteRenderer MakeOverlay(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.enabled = false;
            return sr;
        }

        // ---- test / tooling seam ------------------------------------------------------------------

        /// <summary>True while something is actually being lit. Distinct from "something is offered" —
        /// the suppressions and the renderer lookup both live between the two.</summary>
        public bool IsShowing => _drawn;

        /// <summary>The renderer currently wearing the affordance, or null. What a PlayMode test asserts
        /// on: the RIGHT thing must light, not merely something.</summary>
        public SpriteRenderer Target => _target;

        /// <summary>The state the presenter has settled on this frame. For a test that wants the rule
        /// rather than the plumbing.</summary>
        public InteractAffordanceState State => _state;
    }
}
