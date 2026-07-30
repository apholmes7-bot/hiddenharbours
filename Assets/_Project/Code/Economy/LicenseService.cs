using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// The runtime license wallet (St Peters opening): which fishing/gear licenses the player holds.
    /// Implements the Core <see cref="ILicenseService"/> so Fishing can gate the rod-fishes-cod catch
    /// WITHOUT referencing Economy (it reads <see cref="GameServices.Licenses"/>). Registers itself
    /// there on enable, the IWallet/Save pattern.
    ///
    /// <para><b>Self-installing</b> (like <c>SaveService</c>): a <see cref="RuntimeInitializeOnLoadMethod"/>
    /// creates one persistent instance after the first scene loads, so the licence wallet exists at
    /// runtime with NO scene/builder wiring (Economy doesn't own the builders this wave). The save
    /// bootstraps earlier (BeforeSceneLoad), so the held licences are loaded from it here.</para>
    ///
    /// <para><b>Persistence.</b> The held licenses live in the save (<c>SaveData.OwnedLicenses</c>) so
    /// they survive save/load. This service mirrors that list into a fast in-memory set; when no save
    /// service is present (EditMode / before bootstrap) it works purely in memory, so it's fully
    /// testable headless. A grant is idempotent and persists immediately (like a flag set).</para>
    /// </summary>
    public class LicenseService : MonoBehaviour, ILicenseService
    {
        private static LicenseService _instance;

        // Fast lookup; the source of truth on disk is SaveData.OwnedLicenses (kept in sync on grant).
        private readonly HashSet<string> _held = new();

        /// <summary>Create the persistent license service after the first scene loads (the save service
        /// has bootstrapped by then, BeforeSceneLoad). No scene wiring needed — keeps Economy out of the
        /// builders. Idempotent.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[LicenseService]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LicenseService>();
        }

        private void OnEnable()
        {
            Register();
            // Re-seed when the loaded save becomes authoritative (M1 §7.8's shell). The seed in Register()
            // happens at AfterSceneLoad — which, with a title state in front of play, is BEFORE the player
            // has chosen. Without this, a new game would carry the abandoned game's licences for the whole
            // session, and a tester's "second first-impression" would skip the licence beat entirely.
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            if (ReferenceEquals(GameServices.Licenses, this)) GameServices.Licenses = null;
            if (_instance == this) _instance = null;
        }

        private void OnGameLoaded(GameLoaded _) => ReloadFromSave();

        /// <summary>
        /// Rebuild the in-memory set from the save, discarding anything not in it. The one place that
        /// CLEARS — <see cref="LoadFromSave"/> unions on purpose, which is right at boot and wrong on the
        /// <see cref="GameLoaded"/> edge, where the blob is the whole truth about what is held. Safe on a
        /// resumed game too: the restore grants the saved licences before publishing, so this rebuilds the
        /// identical set. Public so tests can drive it without the play lifecycle.
        /// </summary>
        public void ReloadFromSave()
        {
            _held.Clear();
            LoadFromSave();
        }

        /// <summary>Load the held licences from the save and publish this as <see cref="GameServices.Licenses"/>.
        /// Called from <c>OnEnable</c> at runtime; public so EditMode tests can drive it without the
        /// play-mode lifecycle (EditMode doesn't fire OnEnable for AddComponent — mirrors the
        /// Configure/OnDayStarted public-driver pattern elsewhere in the codebase).</summary>
        public void Register()
        {
            if (_instance == null) _instance = this;
            LoadFromSave();
            GameServices.Licenses = this;
        }

        /// <summary>Seed the in-memory set from the persisted wallet (if a save is loaded). Safe to call
        /// repeatedly; it unions, never clears, so an in-memory grant made before a save attached isn't
        /// lost. Public for tests that drive load explicitly.</summary>
        public void LoadFromSave()
        {
            var data = GameServices.Save?.Current;
            if (data?.OwnedLicenses == null) return;
            for (int i = 0; i < data.OwnedLicenses.Count; i++)
            {
                string id = data.OwnedLicenses[i];
                if (!string.IsNullOrEmpty(id)) _held.Add(id);
            }
        }

        // ---- ILicenseService ----------------------------------------------------------------

        /// <summary>True iff the player holds this license. A null/empty id = "ungated" → true, so a
        /// species that requires no license is always catchable.</summary>
        public bool IsLicensed(string licenseId)
        {
            if (string.IsNullOrEmpty(licenseId)) return true;   // no licence required
            return _held.Contains(licenseId);
        }

        /// <summary>Grant a license (idempotent) and persist it. Does NOT charge money — the vendor
        /// charges via <see cref="IWallet"/> and grants only on a successful spend. No-op on null/empty.</summary>
        public void Grant(string licenseId)
        {
            if (string.IsNullOrEmpty(licenseId)) return;
            if (!_held.Add(licenseId)) return;   // already held — nothing to persist

            var data = GameServices.Save?.Current;
            if (data != null)
            {
                data.OwnedLicenses ??= new List<string>();
                if (!data.OwnedLicenses.Contains(licenseId))
                {
                    data.OwnedLicenses.Add(licenseId);
                    GameServices.Save.Save();
                }
            }
        }

        public int Count => _held.Count;
    }
}
