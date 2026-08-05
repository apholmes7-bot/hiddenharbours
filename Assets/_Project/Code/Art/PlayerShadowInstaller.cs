using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The fisher walks with their own shadow underfoot.</b> A tiny persistent host that attaches ONE
    /// <see cref="SpriteShadow"/> to the on-foot player once it exists — the same sheared, weather-softened,
    /// night-fading shadow the shore plants, the shrubs and (as of the owner's 2026-08-05 casting ruling) the
    /// woods throw. Long west at dawn, a stub at noon, long east at dusk: the player reads the time of day off
    /// the ground they are standing on (P1 "The Sea Has Moods").
    ///
    /// <para><b>Why a host and not a builder line (rule 4).</b> The player is Player-lane code and this is the
    /// Art lane, so the attachment is made from the OUTSIDE, by name, exactly the way
    /// <see cref="PlayerSubmergeInstaller"/> and <c>WadeSplashEmitter</c> already reach the player without
    /// either module referencing the other. It buys more than seam hygiene: a self-installing host cannot be
    /// undone by a scene rebuild the way a builder line can be undone by a scene that was not rebuilt — the
    /// player casts in every scene, in every region, on a fresh clone, with nothing to re-run.</para>
    ///
    /// <para><b>ONE caster covers every state the fisher is drawn in.</b> The walk skin, the iso 8-direction
    /// skin, the haul cycle and the rod-fight poses are all the SAME <see cref="SpriteRenderer"/> on the same
    /// GameObject — they swap its sprite, they do not add renderers — so a single component silhouettes
    /// whichever frame is currently drawn, and the shadow bends as the fisher casts. It also needs no state
    /// gate of its own: <c>ControlSwitcher</c> disables that renderer at the helm, and
    /// <see cref="SpriteShadow"/> already keys its own visibility off <c>caster.enabled</c>, so stepping to
    /// the wheel takes the shadow with it and stepping back ashore brings it straight back.</para>
    ///
    /// <para><b>No tuning, deliberately.</b> The shear is proportional to the caster sprite's own height, so
    /// the character cell's empty headroom cancels out — a shadow raked 5× lands the crown 5 figure-heights
    /// away whether the figure fills the cell or sits in its lower half. And the iso rig's pivot IS the
    /// ground-contact row (the near foot projects below it at the 40° camera, the same way a tree's root
    /// flare does), which is precisely where the shadow anchors. So the defaults are the measurement; there
    /// is nothing here for a number to be wrong about.</para>
    ///
    /// <para><b>Placeholder-safe (rule: null-safe greybox).</b> Whatever the renderer is currently drawing is
    /// what gets silhouetted, including the rectangle some character states still stand in while the rig
    /// builds land. A rectangle's shadow is a correct shadow of a rectangle; the caster inherits the real
    /// sprites the moment they arrive, with no change here.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShadowInstaller : MonoBehaviour
    {
        /// <summary>The persistent player GameObject's name — the one string this seam is built on, shared
        /// with the tests so a rename breaks them rather than silently un-shadowing the fisher.</summary>
        public const string DefaultPlayerObjectName = "Player";

        /// <summary>How often the host looks for the player, in seconds. The player appears within a scene
        /// or two of boot, so this is a handful of polls, not a running cost.</summary>
        private const float PollSeconds = 0.5f;

        [SerializeField] private string _playerObjectName = DefaultPlayerObjectName;
        private float _timer;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("PlayerShadowInstaller") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerShadowInstaller>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollSeconds;

            var go = GameObject.Find(_playerObjectName);
            if (go == null) return;   // not up yet (menu scene / mid-load) — try again

            Attach(go);

            // Whether or not we added it (a builder or a hand-authored prefab may have got there first),
            // our job is done.
            Destroy(gameObject);
        }

        /// <summary>
        /// Give <paramref name="playerGo"/> its shadow, if it draws a sprite and has not got one already.
        /// Returns the caster (existing or new), or null when the object draws nothing to cast.
        ///
        /// <para>Public and static so a test can drive the SAME attachment the host runs rather than a
        /// re-statement of it — the plumbed-but-unread lesson: a guard that exercises its own copy of the
        /// rule proves nothing about the rule that ships.</para>
        /// </summary>
        public static SpriteShadow Attach(GameObject playerGo)
        {
            if (playerGo == null) return null;
            if (playerGo.GetComponent<SpriteRenderer>() == null) return null;

            var existing = playerGo.GetComponent<SpriteShadow>();
            return existing != null ? existing : playerGo.AddComponent<SpriteShadow>();
        }
    }
}
