using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.SpikeDeckCharacterMesh
{
    /// <summary>
    /// ⚠️ SPIKE (deck-character-mesh, draft ADR 0024) — the DEMO rig, never wired into the shipping
    /// player. Attach under a MESH-hull boat's root (the spike editor menu does this): it stands a
    /// mesh character on the deck through the EXISTING facet pipeline and A/Bs it against the
    /// ratcheting 8-direction baked sprite, with the displaced sea on.
    ///
    /// <para><b>How the character rides the deck (the ADR 0023 phase-3 rules, honoured):</b></para>
    /// <list type="bullet">
    ///   <item><b>Pose source is the hull's LIVE renderer</b> — heading dir units, roll, pitch and
    ///   heave pixels are read off the boat's <see cref="IsoFacetHullRenderer"/> AFTER
    ///   <see cref="MeshHullDriver"/> (−110) wrote them and BEFORE the renderers apply (0): this
    ///   component runs at −100. That is the "anchor to the hull's live visual child, not raw world
    ///   positions" requirement: whatever the water lane feeds the hull (wave rock, displaced ride,
    ///   resting draft), the character inherits the same numbers the same frame.</item>
    ///   <item><b>The character is its own facet object</b> (its own hull id, overlay quad and
    ///   SortingGroup), drawn by the SAME off-screen pass into the SAME private z-buffer as its
    ///   hull. Because both roots sit at the same world y, the calibrated iso-depth translation
    ///   (<c>DisplacedWaterMath.HullDepthBias</c>, applied inside <see cref="IsoFacetHullRenderer"/>)
    ///   lands both in ONE commensurate z frame — so hull-vs-character occlusion resolves PER PIXEL
    ///   in the shared buffer, and each overlay composes only its own id's pixels. The deck-height
    ///   lift goes through the HEAVE channel, not the root y, for exactly this reason
    ///   (<see cref="DeckCharacterSpikeMath.SplitAnchor"/>).</item>
    ///   <item><b>Pose = flipbook, heading = transform.</b> One pre-configured renderer per baked
    ///   pose frame; exactly one is active. Swapping active children at the sheet's own frame rate
    ///   costs a registry re-register (trivial); heading/roll/pitch stay CONTINUOUS on the active
    ///   renderer — the smooth weathervane ride the spike exists to demonstrate.</item>
    /// </list>
    ///
    /// <para><b>Dev keys</b> (New Input System — rule: never legacy Input):
    /// J = mesh ↔ sprite A/B · H = idle ↔ hold (fishing stance) · U (hold) = force a slow
    /// weathervane yaw on the boat root. The displaced-sea A/B stays on its own key (O, the water
    /// lane's toggle).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]   // after MeshHullDriver (−110), before the facet renderers (0)
    public sealed class DeckCharacterMeshSpikeRig : MonoBehaviour
    {
        [Tooltip("The baked mesh flipbook (bake it via Hidden Harbours → Spike → Deck Character Mesh).")]
        [SerializeField] private DeckCharacterMeshSpikeDef _def;

        [Tooltip("The baked 8-dir sprite skin for the ratcheting half of the A/B (FisherIso).")]
        [SerializeField] private CharacterVisualDef _spriteVisual;

        [Tooltip("Where the character stands, in boat-local metres (rig axes: +X starboard, +Y bow, " +
                 "+Z up; rig z=0 is the keel bottom). Default: the lobster boat's aft working deck.")]
        [SerializeField] private Vector3 _deckLocalMetres = new Vector3(0.35f, -1.6f, 1.35f);

        [Tooltip("Deck-local heading in degrees (0 = facing the bow). The character keeps this " +
                 "relative heading while the hull weathervanes — deck-space facing, §4.1.")]
        [SerializeField] private float _localHeadingDegrees = 205f;

        [Tooltip("Forced weathervane rate while U is held (deg/s). Dev-only demo crutch for when " +
                 "the ambient drift is too polite.")]
        [SerializeField] private float _yawDegreesPerSecond = 8f;

        [SerializeField] private Key _abKey = Key.J;
        [SerializeField] private Key _animKey = Key.H;
        [SerializeField] private Key _yawKey = Key.U;

        private Transform _boatRoot;
        private IsoFacetHullRenderer _hull;
        private readonly List<IsoFacetHullRenderer> _poseRenderers = new List<IsoFacetHullRenderer>();
        private readonly List<int> _clipStart = new List<int>();   // first renderer index per clip
        private GameObject _spriteGo;
        private SpriteRenderer _sprite;

        private bool _useMesh = true;
        private int _clip;
        private double _clock;
        private int _activeIndex = -1;
        private bool _warned;

        private void Start()
        {
            _boatRoot = transform.parent;
            if (_boatRoot == null)
            {
                Debug.LogWarning("[deck-char SPIKE] Rig must be parented under a boat root. Idle.");
                enabled = false;
                return;
            }

            // The BOAT's facet renderer — found before this rig builds its own, so the search can
            // never land on a character pose renderer.
            foreach (var r in _boatRoot.GetComponentsInChildren<IsoFacetHullRenderer>(true))
                if (r.IsConfigured && !r.transform.IsChildOf(transform)) { _hull = r; break; }

            if (_hull == null)
            {
                Debug.LogWarning("[deck-char SPIKE] No configured mesh-hull renderer on this boat — " +
                                 "the demo needs a MESH hull (lobster boat / side dragger). Idle.");
                enabled = false;
                return;
            }

            if (_def == null || !_def.IsUsable())
            {
                Debug.LogWarning("[deck-char SPIKE] No usable DeckCharacterMeshSpikeDef — run the " +
                                 "spike bake menu first. Idle.");
                enabled = false;
                return;
            }

            BuildPoseRenderers();
            BuildSprite();
            Debug.Log("[deck-char SPIKE] Ready. J = mesh↔sprite A/B, H = idle↔hold, U (hold) = " +
                      "force weathervane, O = displaced sea (water lane's own toggle).");
        }

        private void BuildPoseRenderers()
        {
            for (int c = 0; c < _def.Clips.Length; c++)
            {
                _clipStart.Add(_poseRenderers.Count);
                var clip = _def.Clips[c];
                for (int f = 0; f < clip.Frames.Length; f++)
                {
                    var go = new GameObject($"SpikeCharPose_{clip.Anim}_{f}")
                    {
                        hideFlags = HideFlags.DontSave,
                    };
                    go.transform.SetParent(transform, false);
                    var r = go.AddComponent<IsoFacetHullRenderer>();
                    r.Configure(_def.BuildSetup(clip.Frames[f]));
                    go.SetActive(false);
                    _poseRenderers.Add(r);
                }
            }
        }

        private void BuildSprite()
        {
            _spriteGo = new GameObject("SpikeCharSprite") { hideFlags = HideFlags.DontSave };
            _spriteGo.transform.SetParent(transform, false);
            _sprite = _spriteGo.AddComponent<SpriteRenderer>();
            _spriteGo.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_hull == null || !_hull.IsConfigured)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[deck-char SPIKE] Hull renderer went away (hull swap?). Idle.");
                }
                return;
            }

            HandleKeys();

            // ---- the deck under our feet, as the hull is DRAWN this frame -------------------
            float hullDir = _hull.HeadingDirUnits;
            float hullRoll = _hull.RollDegrees;
            float hullPitch = _hull.PitchDegrees;
            float hullHeavePx = _hull.HeavePixels;
            float elevRad = _def.ElevationDeg * Mathf.Deg2Rad;
            float turntableRad = hullDir * (Mathf.PI / 4f);

            // Where the deck anchor is on screen for THIS heading/rock — the mounted-overlay
            // projection every deck prop already uses (motor, tote), so the character swings with
            // the rock about the hull origin instead of bouncing anti-phase (the overlay-pose
            // lesson).
            Vector2 anchor = MountedRockPoseMath.Project(
                _deckLocalMetres, turntableRad,
                hullRoll * Mathf.Deg2Rad, hullPitch * Mathf.Deg2Rad, elevRad);
            DeckCharacterSpikeMath.SplitAnchor(anchor, _def.PxPerMetre,
                                               out float rootOffsetX, out float anchorHeavePx);

            // Root: the hull's own ground anchor (same world y ⇒ same calibrated iso-z frame while
            // the displaced sea is live), offset only along screen-x. Rotation is stomped to screen
            // identity every frame exactly as the hull's visual child is — the parent physics yaw
            // must not double the turn.
            transform.rotation = Quaternion.identity;
            Vector3 rootPos = _boatRoot.position;
            rootPos.x += rootOffsetX;
            transform.position = rootPos;

            // World-compass heading of the character = the boat's true heading + the deck-local
            // heading (deck-space facing, §4.1 — input would write _localHeadingDegrees, the
            // weathervane writes the boat's).
            float boatHeadingDeg = DirectionalBoatSprite.HeadingDegreesFromBow(_boatRoot.up);
            float worldHeadingDeg = boatHeadingDeg + _localHeadingDegrees;

            // ---- pose clock -----------------------------------------------------------------
            _clock += Time.deltaTime;
            var clip = _def.Clips[_clip];
            int frame = DeckCharacterSpikeMath.PoseFrame(_clock, clip.Frames.Length,
                                                         clip.FramesPerSecond);

            int sortLayer = 0, sortOrder = 1;
            var hullOverlay = _hull.OverlayRenderer;
            if (hullOverlay != null)
            {
                sortLayer = hullOverlay.sortingLayerID;
                sortOrder = hullOverlay.sortingOrder + 1;
            }

            if (_useMesh)
            {
                int index = _clipStart[_clip] + frame;
                SetActivePose(index);

                var r = _poseRenderers[index];
                r.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(
                    worldHeadingDeg, 0f, _def.AzimuthCounterClockwise);
                DeckCharacterSpikeMath.DeckTiltToCharacter(hullRoll, hullPitch, _localHeadingDegrees,
                                                           out float rollC, out float pitchC);
                r.RollDegrees = rollC;
                r.PitchDegrees = pitchC;
                r.HeavePixels = hullHeavePx + anchorHeavePx;
                r.SetSorting(sortLayer, sortOrder);
            }
            else if (_sprite != null && _spriteVisual != null)
            {
                // The ratcheting control: today's shipping representation. Row snapped to 45°,
                // frames from the idle sheet, no live tilt (a baked cell cannot tilt), the deck
                // lift ridden as a plain screen offset.
                int row = _spriteVisual.FacingRowFor(worldHeadingDeg);
                int spriteFrame = DeckCharacterSpikeMath.PoseFrame(
                    _clock, _spriteVisual.IdleFrameCount, _spriteVisual.IdleFramesPerSecond);
                int cell = row * _spriteVisual.IdleFrameCount + spriteFrame;
                if (_spriteVisual.IdleSheet != null && cell < _spriteVisual.IdleSheet.Length)
                    _sprite.sprite = _spriteVisual.IdleSheet[cell];
                _sprite.sortingLayerID = sortLayer;
                _sprite.sortingOrder = sortOrder;
                _spriteGo.transform.rotation = Quaternion.identity;
                _spriteGo.transform.position = transform.position + new Vector3(
                    0f, (hullHeavePx + anchorHeavePx) / _def.PxPerMetre, 0f);
            }
        }

        private void SetActivePose(int index)
        {
            if (index == _activeIndex) return;
            if (_activeIndex >= 0 && _activeIndex < _poseRenderers.Count)
                _poseRenderers[_activeIndex].gameObject.SetActive(false);
            _poseRenderers[index].gameObject.SetActive(true);
            _activeIndex = index;
        }

        private void SetMesh(bool useMesh)
        {
            _useMesh = useMesh;
            if (!useMesh && _activeIndex >= 0)
            {
                _poseRenderers[_activeIndex].gameObject.SetActive(false);
                _activeIndex = -1;
            }
            if (_spriteGo != null) _spriteGo.SetActive(!useMesh);
            Debug.Log($"[deck-char SPIKE] A/B → {(useMesh ? "MESH (continuous)" : "SPRITE (8-dir ratchet)")}");
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_abKey].wasPressedThisFrame)
                SetMesh(!_useMesh);

            if (kb[_animKey].wasPressedThisFrame && _def.Clips.Length > 1)
            {
                _clip = (_clip + 1) % _def.Clips.Length;
                _clock = 0;
                Debug.Log($"[deck-char SPIKE] anim → {_def.Clips[_clip].Anim}");
            }

            if (kb[_yawKey].isPressed && _boatRoot != null)
            {
                // Demo crutch: turn the deck under the character. Rotating the root is what every
                // heading consumer reads (MeshHullDriver reads transform.up next frame). A dynamic
                // Rigidbody2D gets its rotation written too so physics does not immediately unwind
                // the turn.
                float step = _yawDegreesPerSecond * Time.deltaTime;
                var rb = _boatRoot.GetComponent<Rigidbody2D>();
                if (rb != null) rb.MoveRotation(rb.rotation - step);
                else _boatRoot.Rotate(0f, 0f, -step);
            }
        }

        private void OnDestroy()
        {
            foreach (var r in _poseRenderers)
                if (r != null) Destroy(r.gameObject);
            _poseRenderers.Clear();
            if (_spriteGo != null) Destroy(_spriteGo);
        }
    }
}
