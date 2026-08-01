namespace HiddenHarbours.Core
{
    /// <summary>
    /// The player's mix — the four independent bus volumes the settings sheet drives (M1 §7.8, and the
    /// M1 DoD's promise of "independent volume sliders", which until now had no player-facing surface).
    ///
    /// <para><b>Why it is a Core contract.</b> The volumes live on <c>Audio.AudioDirector</c>'s serialized
    /// fields, and the sliders live in UI. UI references only Core, which structurally prevents it reaching
    /// into the Audio module — so the mix comes through here, the same indirection as
    /// <see cref="IWallet"/> and <see cref="ILicenseService"/> (rule 4).</para>
    ///
    /// <para><b>Live, not deferred.</b> A setter takes effect on the next mix pass, so dragging a slider
    /// moves the sound under your hand — which is the only way a volume slider can be judged. Persistence
    /// is a separate concern (<see cref="GameSettings"/>): this interface is the running mix, not the
    /// stored one.</para>
    ///
    /// <para>Values are 0..1 and implementations clamp — a settings sheet, a console command or a future
    /// accessibility preset cannot push a bus out of range. OPTIONAL and NOT part of
    /// <see cref="GameServices.Ready"/>: null before the director installs (EditMode, a stripped build), so
    /// consumers must null-check.</para>
    /// </summary>
    public interface IAudioMix
    {
        /// <summary>The master fader every other bus multiplies through.</summary>
        float MasterVolume { get; set; }

        /// <summary>Sea, gulls, wind, the aboard propulsion beds — the world's own sound.</summary>
        float AmbienceVolume { get; set; }

        /// <summary>One-shots: the catch sting, the made-it-home warmth.</summary>
        float SfxVolume { get; set; }

        /// <summary>The music bus. Live (and ducking) since VS-27, with no stem in it yet.</summary>
        float MusicVolume { get; set; }
    }
}
