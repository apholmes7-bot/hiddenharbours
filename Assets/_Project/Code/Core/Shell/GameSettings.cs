using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The player's settings, and where they are kept (M1 §7.8): the four bus volumes and
    /// fullscreen/windowed.
    ///
    /// <para><b>Why NOT the save file.</b> These belong to the person and the machine, not to the harbour.
    /// A volume slider must survive New Game, must not travel with a copied save, and must not cost a
    /// schema version — so they live in <see cref="PlayerPrefs"/> while game state lives in
    /// <see cref="SaveData"/>. (The onboarding flags went the other way, off PlayerPrefs and INTO the save,
    /// for exactly the mirror-image reason: those are facts about a playthrough.) The save schema is
    /// untouched by the shell.</para>
    ///
    /// <para><b>Nothing is written until the player changes something.</b> An unstored setting means "use
    /// the authored default" — the director's own serialized fields, and whatever the player launched the
    /// window as — so a first run is not silently overridden by a file of zeros.</para>
    /// </summary>
    public static class GameSettings
    {
        // Stable, append-only keys. Prefixed so they cannot collide with anything else in the prefs.
        public const string MasterKey     = "hh.audio.master";
        public const string AmbienceKey   = "hh.audio.ambience";
        public const string SfxKey        = "hh.audio.sfx";
        public const string MusicKey      = "hh.audio.music";
        public const string FullscreenKey = "hh.display.fullscreen";

        /// <summary>Has the player ever set a volume? If not, the director's authored defaults stand.</summary>
        public static bool HasStoredMix => PlayerPrefs.HasKey(MasterKey);

        /// <summary>Has the player ever chosen fullscreen/windowed? If not, the launch mode stands.</summary>
        public static bool HasStoredDisplay => PlayerPrefs.HasKey(FullscreenKey);

        /// <summary>
        /// Push the stored mix into a live <paramref name="mix"/>. A no-op when nothing is stored, so the
        /// authored defaults survive a first run. Null-safe.
        /// </summary>
        public static void LoadInto(IAudioMix mix)
        {
            if (mix == null || !HasStoredMix) return;
            mix.MasterVolume   = PlayerPrefs.GetFloat(MasterKey,   mix.MasterVolume);
            mix.AmbienceVolume = PlayerPrefs.GetFloat(AmbienceKey, mix.AmbienceVolume);
            mix.SfxVolume      = PlayerPrefs.GetFloat(SfxKey,      mix.SfxVolume);
            mix.MusicVolume    = PlayerPrefs.GetFloat(MusicKey,    mix.MusicVolume);
        }

        /// <summary>
        /// Remember the live mix. Called when the settings sheet is put away rather than on every frame of
        /// a drag — the sound moves live off <see cref="IAudioMix"/>; only the writing is deferred.
        /// Null-safe.
        /// </summary>
        public static void StoreFrom(IAudioMix mix)
        {
            if (mix == null) return;
            PlayerPrefs.SetFloat(MasterKey,   mix.MasterVolume);
            PlayerPrefs.SetFloat(AmbienceKey, mix.AmbienceVolume);
            PlayerPrefs.SetFloat(SfxKey,      mix.SfxVolume);
            PlayerPrefs.SetFloat(MusicKey,    mix.MusicVolume);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Fullscreen or windowed. Reading reports what the screen is ACTUALLY doing (the truth, not a
        /// remembered intention); writing applies it and remembers it.
        ///
        /// <para><see cref="FullScreenMode.FullScreenWindow"/> — borderless — rather than exclusive:
        /// alt-tabbing out of a cozy game to look something up should not black the screen for three
        /// seconds. This is the whole of the M1 display offer; graphics presets are deliberately not M1.</para>
        /// </summary>
        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set
            {
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Re-apply the stored display choice at launch. Does nothing if the player has never chosen —
        /// then however the build started is what they get.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void ApplyStoredDisplay()
        {
            if (!HasStoredDisplay) return;
            bool full = PlayerPrefs.GetInt(FullscreenKey, 0) != 0;
            Screen.fullScreenMode = full ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }
}
