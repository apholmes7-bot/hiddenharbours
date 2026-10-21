namespace HiddenHarbours.Core
{
    /// <summary>The bridge publishes the same phase-baked trains it sends to the shader.
    /// Riders and seakeeping sample them at time zero. The field is recomputed from world time
    /// through WaveMemory; publication shares the exact rendered tick. Nothing is saved.
    /// Before publication consumers derive the same memory field through IWaveWindSource,
    /// or use a steady current wind in fixtures without that seam (ADR 0018).</summary>
    public static class SharedWaveField
    {
        private static object s_Owner;
        private static WaveTrains s_Trains;

        /// <summary>True while a publisher has handed over the eased field — the gate a rider uses
        /// to choose the shared sea over its own local animator.</summary>
        public static bool IsPublished => s_Owner != null;

        /// <summary>
        /// The published eased trains, to be sampled at <c>timeSeconds = 0</c> (see the class doc).
        /// False (and <see cref="WaveTrains.None"/>) when nothing has published — the fallback
        /// contract.
        /// </summary>
        public static bool TryGet(out WaveTrains trains)
        {
            trains = s_Trains;
            return s_Owner != null;
        }

        /// <summary>Publish the eased field for this tick — called by the bridge with the SAME
        /// trains it packs into the shader globals, immediately around that push, so the sea the
        /// riders read and the sea the shader draws cannot be one tick apart. One sea per region:
        /// with multiple publishers the last one wins, exactly like <see cref="DisplacedSea"/>.</summary>
        public static void Publish(object owner, in WaveTrains trains)
        {
            if (owner == null) return;
            s_Owner = owner;
            s_Trains = trains;
        }

        /// <summary>Clear the field — only by its current owner, so a stale publisher going away
        /// cannot silence a newer one. Riders fall back to their local animators, which is the
        /// pre-seam behaviour exactly.</summary>
        public static void Clear(object owner)
        {
            if (!ReferenceEquals(s_Owner, owner)) return;
            s_Owner = null;
            s_Trains = WaveTrains.None;
        }
    }
}
