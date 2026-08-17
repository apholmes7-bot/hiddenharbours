using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The fisher never gets lost in the woods.</b> A tiny persistent host that attaches ONE
    /// <see cref="SilhouetteMaskSource"/> to the on-foot player once it exists, so dense forest can
    /// draw in front of her without swallowing her (owner ruling, 2026-08-16: "slight occlusion — you
    /// always know where you are, you never lose your character").
    ///
    /// <para><b>Why a host and not a builder line (rule 4).</b> Verbatim the argument
    /// <see cref="PlayerShadowInstaller"/> makes, and this is its twin in every respect: the player is
    /// Player-lane code and this is the Art lane, so the attachment is made from the OUTSIDE, by name,
    /// the same way <c>PlayerSubmergeInstaller</c> and <c>WadeSplashEmitter</c> already reach the
    /// player without either module referencing the other. It buys more than seam hygiene — a
    /// self-installing host cannot be undone by a scene rebuild the way a builder line can be undone
    /// by a scene that was not rebuilt, and these two regions' scenes are BANKED. The fisher reads
    /// through foliage in every scene, in every region, on a fresh clone, with nothing to re-run and
    /// no Build click owed.</para>
    ///
    /// <para><b>Region-agnostic, by construction.</b> Nothing here knows about St Peters or Nine Mile
    /// Creek, or about trees at all. The mask says "here is the body"; the foliage shaders decide what
    /// to do about it. That is why this ships BEFORE the density pass rather than after: the woods can
    /// then be made as deep as the owner likes without a single frame in which the fisher is lost
    /// behind them.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSilhouetteInstaller : MonoBehaviour
    {
        /// <summary>The persistent player GameObject's name — the one string this seam is built on,
        /// shared with <see cref="PlayerShadowInstaller.DefaultPlayerObjectName"/> so the two hosts can
        /// never disagree about who the fisher is, and with the tests so a rename breaks them rather
        /// than silently un-silhouetting her.</summary>
        public const string DefaultPlayerObjectName = PlayerShadowInstaller.DefaultPlayerObjectName;

        /// <summary>How often the host looks for the player, in seconds. The player appears within a
        /// scene or two of boot, so this is a handful of polls, not a running cost.</summary>
        private const float PollSeconds = 0.5f;

        [SerializeField] private string _playerObjectName = DefaultPlayerObjectName;
        private float _timer;

        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("PlayerSilhouetteInstaller") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerSilhouetteInstaller>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollSeconds;

            var go = GameObject.Find(_playerObjectName);
            if (go == null) return;   // not up yet (menu scene / mid-load) — try again

            Attach(go);

            // Whether or not we added it (a builder or a hand-authored prefab may have got there
            // first), our job is done.
            Destroy(gameObject);
        }

        /// <summary>
        /// Give <paramref name="playerGo"/> its silhouette source, if it draws a sprite and has not got
        /// one already. Returns the source (existing or new), or null when the object draws nothing to
        /// silhouette.
        ///
        /// <para>Public and static so a test can drive the SAME attachment the host runs rather than a
        /// re-statement of it — the plumbed-but-unread lesson: a guard that exercises its own copy of
        /// the rule proves nothing about the rule that ships.</para>
        /// </summary>
        public static SilhouetteMaskSource Attach(GameObject playerGo)
        {
            if (playerGo == null) return null;
            if (playerGo.GetComponent<SpriteRenderer>() == null) return null;

            var existing = playerGo.GetComponent<SilhouetteMaskSource>();
            return existing != null ? existing : playerGo.AddComponent<SilhouetteMaskSource>();
        }
    }
}
