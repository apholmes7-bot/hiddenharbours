using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A LIGHT IN HER HAND</b> — the walker's own two lamps (world-lighting PR 3).
    ///
    /// <para>The owner, 2026-09-03: <i>"the player has no spotlight which reaches the trees"</i> ·
    /// <i>"a latern and a spotlight, and yes i want lights on land"</i>. The land lamps (PR 2) lit the
    /// places somebody built a lamp post; everywhere else — the woods, the shore, the road between the
    /// two — she still walked in the dark carrying nothing.</para>
    ///
    /// <para><b>Two lamps, and the difference between them is the point.</b>
    /// <list type="bullet">
    ///   <item><b>The LANTERN</b> is ambient, automatic and costs her nothing: slung on her back, alight
    ///   after dusk without a keypress, a warm pool at her feet. It is the light you walk home by.</item>
    ///   <item><b>The HEADLAMP</b> is directed and switched: a narrow cone that goes where she looks,
    ///   thrown far enough to pick a trunk out of a wood. It is the light you look for something
    ///   with.</item>
    /// </list></para>
    ///
    /// <para><b>Neither takes a new key</b> — the dev key ledger is exhausted. The headlamp answers the
    /// SAME key the boat's searchlight does, and the two can never both hear it: see
    /// <see cref="WalksHerOwnBeam"/>, where the partition is proved rather than arranged.</para>
    ///
    /// <para><b>Why a self-installing host rather than a component on the player prefab.</b> The same
    /// reason <see cref="PlayerShadowInstaller"/> is one: the player is spawned by the shell, not placed
    /// in a scene, so there is no prefab in a builder to hang this off — and a light that needed one
    /// would be a light the owner's next Build could lose. It installs itself, finds her through Core,
    /// and parents its two lamps under her.</para>
    ///
    /// <para><b>⭐ And the parenting is load-bearing, not tidiness.</b> Her own <c>SpriteShadow</c> lives
    /// on the player root (<see cref="PlayerShadowInstaller"/> puts it there), so hanging the lamps
    /// beneath her is what lets <see cref="LampShadowSystem"/>'s carrier rule recognise her as the thing
    /// they are MOUNTED ON and decline to throw her own silhouette down her own beam. Free-standing
    /// objects tracking her position would light identically and shadow her absurdly.</para>
    ///
    /// <para><b>Rule 4.</b> Art may not name a Player type. Everything this needs about her — where she
    /// is, which way she faces, and whether she is on her own two feet — arrives through Core's
    /// <see cref="InteractActorProbe"/>, which <c>ControlSwitcher</c> publishes every frame for exactly
    /// this kind of reader, and through <see cref="GameServices.PlayerTransform"/>.</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Nothing is saved and nothing is random: the lantern's flicker is
    /// <see cref="SceneLight"/>'s own deterministic hash of (seed, time), the night gate is in the shader
    /// off the published tint, and the beam's on/off is a transient the player sets.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalkerLights : MonoBehaviour
    {
        // ---- the mounting, in metres up her body ------------------------------------------------
        // Both are BLOOM LIFTS (SceneLight.BloomLiftMetres): the height the lit fitting itself hangs at,
        // which is what #733 made the glow's home. The POOL each throws still lands on the ground at her
        // feet, which is where a pool belongs and where LampPoolSystem draws it.

        /// <summary>Height of the lantern on her back-sling, metres. The rod-sling anchor
        /// (<c>CarryMountKind.Back</c>) sits about here on a 1.7 m character.</summary>
        public const float LanternLiftMetres = 1.05f;

        /// <summary>Height of the headlamp on her brow, metres.</summary>
        public const float HeadlampLiftMetres = 1.55f;

        /// <summary>Half-angle of the headlamp cone. Narrow — a headlamp you can aim is the point, and a
        /// wide one is just a worse lantern. (The boat's searchlight is 26°; hers is tighter because it is
        /// a lamp on a band, not a searchlight on a mounting.)</summary>
        public const float HeadlampConeHalfDegrees = 21f;

        /// <summary>The key the beam answers — the SAME one the boat's searchlight uses, deliberately.
        /// See <see cref="WalksHerOwnBeam"/> for why that is safe.</summary>
        public const Key ToggleKey = Key.L;

        private SceneLight _lantern;
        private Headlamp _headlamp;

        /// <summary>The carried lantern (null before wake).</summary>
        public SceneLight Lantern => _lantern;

        /// <summary>The worn headlamp (null before wake).</summary>
        public Headlamp Beam => _headlamp;

        // =============================================================================================
        //  ⭐ WHOSE BEAM ANSWERS THE KEY — the partition, and why it needs no arbitration
        // =============================================================================================

        /// <summary>
        /// <b>Is the walker the one whose beam answers the switch right now?</b> True exactly when she is
        /// on her own two feet ashore.
        ///
        /// <para><b>⭐ The charter asked that "only ONE of the two beams may answer the key at a time", and
        /// this delivers it BY CONSTRUCTION rather than by arbitrating.</b> The boat's searchlight answers
        /// the key when <c>BoatSpotlight.PlayerSwitchesThisBeam</c> is true, which is
        /// <c>GameServices.Helm.IsPlayersBoat(...)</c> — and <c>ControlSwitcher</c> sets the player's boat
        /// to non-null for exactly <c>ControlMode.Aboard || ControlMode.OnDeck</c>. This reads the SAME
        /// underlying mode through Core's actor probe and answers for <c>OnFoot</c>. The two predicates
        /// therefore partition <see cref="ControlMode"/> and cannot both hold — not because anything
        /// negotiates, but because they are two halves of one enum. <c>Driving</c> belongs to neither: a
        /// road vehicle's headlights are the road fleet's, not hers.</para>
        ///
        /// <para>The partition is asserted over every member of <c>ControlMode</c> rather than trusted to
        /// this paragraph — the enum is append-only Core contract, and the next mode added to it would
        /// otherwise silently belong to nobody or to both.</para>
        /// </summary>
        public static bool WalksHerOwnBeam =>
            InteractActorProbe.Has && InteractActorProbe.Current.Context == InteractContext.OnFoot;

        /// <summary>
        /// <b>Does the walker own the decor light singleton this frame?</b> True when she is walking her
        /// own beam AND that beam is lit.
        ///
        /// <para><b>⚠️ This one DOES need a guard, and the charter's "one publisher" is why.</b> The lit
        /// decor path (<c>SpriteLitDecor.hlsl</c>) lights trees, shrubs and shore plants from ONE global
        /// lamp — <c>_BoatLightPos</c>/<c>_BoatLightDir</c> — and every <see cref="BoatSpotlight"/> in the
        /// scene writes it every frame, INCLUDING a boat whose beam is off, which writes zeros. So a
        /// moored dory forty metres away would blank her headlamp's contribution on whichever frames its
        /// Update ran last: no error, no warning, and the trees in front of her flickering unlit by script
        /// order. <see cref="BoatSpotlight"/> therefore stands off the singleton while this is true — and
        /// <see cref="Headlamp"/>'s <c>LateUpdate</c> writes it in their place. Those two are ONE change and
        /// neither is correct alone: standing the boats off without putting her lamp there would freeze the
        /// trees under whatever a boat wrote last, which reads as woods lit by a lamp that is not there.</para>
        ///
        /// <para>It is the narrowest guard that closes it: the boat's own water-light array slot is
        /// untouched, so the sea is lit exactly as before, and at the helm — where she cannot be on foot —
        /// this is false and the searchlight's every byte is unchanged.</para>
        /// </summary>
        public static bool OwnsDecorLight { get; private set; }

        // =============================================================================================
        //  INSTALL
        // =============================================================================================

        private static bool _installed;

        /// <summary>
        /// Self-install after the first scene loads, the shape every ambient Art system in this project
        /// uses (<see cref="PlayerShadowInstaller"/>, <c>DayNightController</c>). The player may not exist
        /// yet — the host waits for her rather than giving up.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("WalkerLights") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<WalkerLights>();
        }

        /// <summary>For fixtures: forget that the runtime host was installed.</summary>
        public static void ResetInstallForTests()
        {
            _installed = false;
            OwnsDecorLight = false;
        }

        /// <summary>
        /// For fixtures: declare who owns the decor lamp this frame, without running one.
        ///
        /// <para>The flag is assigned in <c>Update</c>, and EditMode runs no Unity messages — so the two
        /// halves of "one publisher" (the boats standing off, and <see cref="Headlamp"/> writing in their
        /// place) could otherwise only be asserted in PlayMode, where they would be a render question
        /// rather than a rule. <see cref="ResetInstallForTests"/> clears it again.</para>
        /// </summary>
        public static void SetOwnsDecorLightForTests(bool owns) => OwnsDecorLight = owns;

        private void OnDisable() => OwnsDecorLight = false;

        private void Update()
        {
            Transform her = FindWalker();
            if (her == null) { OwnsDecorLight = false; return; }
            if (_lantern == null || _headlamp == null) Build(her);

            bool onFoot = WalksHerOwnBeam;

            // The SWITCH, read the way BoatSpotlight reads it — New Input System only (Keyboard.current);
            // legacy UnityEngine.Input compiles in this project and then throws at runtime.
            //
            // ⭐ ONE PLACE, so the input-intents lane (ADR 0043) can lift it. When WalkIntents grows a lamp
            // toggle this is the only line that changes; nothing else here knows a key exists.
            if (onFoot && _headlamp != null)
            {
                var kb = Keyboard.current;
                if (kb != null && kb[ToggleKey].wasPressedThisFrame) _headlamp.Toggle();
            }

            // Both lamps are hers only while she is on her own two feet ashore. On a deck or at a wheel
            // the boat's own lights are the picture (boat-lights PR 2a-2c), and a carried pool on a lit
            // deck is noise; it is also the one rule that keeps the key partition and the lamps agreeing
            // about what "on foot" means.
            if (_lantern != null) _lantern.enabled = onFoot;
            if (_headlamp != null) _headlamp.SetLive(onFoot);

            OwnsDecorLight = onFoot && _headlamp != null && _headlamp.IsOn;

            // Aim: she is the actor, so the probe's facing IS the beam's axis. SceneLight throws its cone
            // along transform.up, which is the same contract BoatSpotlight aims by.
            if (_headlamp != null && onFoot)
            {
                Vector2 facing = InteractActorProbe.Current.Facing;
                if (facing.sqrMagnitude > 1e-6f)
                    _headlamp.transform.up = new Vector3(facing.x, facing.y, 0f);
            }
        }

        /// <summary>
        /// Her transform, through Core. <see cref="GameServices.PlayerTransform"/> is the published seam;
        /// the name lookup is the same fallback the sibling installers keep, for a bare art scene where
        /// nothing has claimed the slot.
        ///
        /// <para>⚠️ <c>!= null</c> and not <c>?.</c> — a destroyed Unity object is fake-null, and the
        /// null-conditional operator does not see it (<c>GameServices.PlayerTransform</c>'s own note).</para>
        /// </summary>
        private static Transform FindWalker()
        {
            Transform t = GameServices.PlayerTransform;
            if (t != null) return t;
            var go = GameObject.Find(PlayerShadowInstaller.DefaultPlayerObjectName);
            return go != null ? go.transform : null;
        }

        /// <summary>
        /// Hang the two lamps under her. Parenting is what makes the carrier rule work — see the class
        /// note — so this is never a free-standing object tracking her position.
        /// </summary>
        private void Build(Transform her)
        {
            if (_lantern == null)
            {
                var go = new GameObject("WalkerLantern");
                go.transform.SetParent(her, worldPositionStays: false);
                _lantern = go.AddComponent<SceneLight>();
                LightPresets.Apply(_lantern, LightPresets.Kind.Lantern);
                _lantern.ReachMetres = LightPresets.ReachMetres(LightPresets.Kind.Lantern);
                _lantern.BloomLiftMetres = LanternLiftMetres;
                _lantern.LampHeightMeters = LanternLiftMetres;   // what its cast shadows are thrown from
                _lantern.CastsShadows = true;
            }

            if (_headlamp == null)
            {
                var go = new GameObject("WalkerHeadlamp");
                go.transform.SetParent(her, worldPositionStays: false);
                _headlamp = go.AddComponent<Headlamp>();
                _headlamp.Configure(HeadlampConeHalfDegrees, HeadlampLiftMetres);
            }
        }
    }
}
